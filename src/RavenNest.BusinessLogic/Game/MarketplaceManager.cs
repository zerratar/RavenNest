using System;
using System.Collections.Generic;
using System.Linq;
using RavenNest.BusinessLogic.Data;
using RavenNest.DataModels;
using RavenNest.Models;
using InventoryItem = RavenNest.DataModels.InventoryItem;

namespace RavenNest.BusinessLogic.Game
{
    public class MarketplaceManager : IMarketplaceManager
    {
        private readonly IPlayerManager playerManager;
        private readonly IGameData gameData;

        public MarketplaceManager(
            IPlayerManager playerManager,
            IGameData gameData)
        {
            this.playerManager = playerManager;
            this.gameData = gameData;
        }

        public MarketItemCollection GetItems(int offset, int size)
        {
            var collection = new MarketItemCollection();
            var marketItemCount = gameData.GetMarketItemCount();
            var items = gameData.GetMarketItems(offset, size);

            collection.Offset = offset;
            collection.Total = marketItemCount;
            collection.AddRange(
                items.Select(x =>
                {
                    var character = gameData.GetCharacter(x.SellerCharacterId);
                    var user = gameData.GetUser(character.UserId);
                    var item = DataMapper.Map<Models.MarketItem, DataModels.MarketItem>(x);
                    item.SellerUserId = user.UserId;
                    return item;
                }));

            return collection;
        }

        public ItemSellResult SellItem(
            SessionToken token, string userId, Guid itemId, long amount, decimal pricePerItem)
        {
            if (amount <= 0 || pricePerItem <= 0)
            {
                return new ItemSellResult(ItemTradeState.RequestToLow);
            }

            var character = GetCharacterAsync(token, userId);
            if (character == null) return new ItemSellResult(ItemTradeState.Failed);

            if (!playerManager.AcquiredUserLock(token, character))
            {
                return new ItemSellResult(ItemTradeState.Failed);
            }

            var itemsToSell = gameData.GetInventoryItems(character.Id, itemId);
            //var itemsToSell = character.InventoryItem
            //    .Where(x => x.ItemId == itemId && !x.Equipped)
            //    .ToList();

            var totalItemCount = itemsToSell.Count > 0 ? itemsToSell.Sum(x => x.Amount.GetValueOrDefault()) : 0;
            var newItemAmount = totalItemCount - amount;

            if (itemsToSell.Count == 0 || newItemAmount < 0)
            {
                return new ItemSellResult(ItemTradeState.DoesNotOwn);
            }

            gameData.RemoveRange(itemsToSell);

            if (newItemAmount > 0)
            {
                var mergedInventoryItem = new InventoryItem
                {
                    Id = Guid.NewGuid(),
                    Amount = newItemAmount,
                    CharacterId = character.Id,
                    Equipped = false,
                    ItemId = itemId,
                };
                gameData.Add(mergedInventoryItem);
            }

            var marketItem = new DataModels.MarketItem
            {
                Id = Guid.NewGuid(),
                Amount = amount,
                Created = DateTime.UtcNow,
                ItemId = itemId,
                PricePerItem = pricePerItem,
                SellerCharacterId = character.Id,
            };

            gameData.Add(marketItem);

            return new ItemSellResult(ItemTradeState.Success);
        }

        public ItemBuyResult BuyItem(
            SessionToken token, string userId, Guid itemId, long amount, decimal maxPricePerItem)
        {
            var boughtPricePerItem = new List<decimal>();
            var boughtItemCount = new List<long>();
            var boughtTotalCost = 0m;
            var boughtTotalAmount = 0L;

            if (amount <= 0 || maxPricePerItem <= 0)
            {
                return new ItemBuyResult(ItemTradeState.RequestToLow, new long[0], new decimal[0], 0, 0);
            }

            var character = GetCharacterAsync(token, userId);
            if (character == null) return new ItemBuyResult(ItemTradeState.Failed, new long[0], new decimal[0], 0, 0);

            if (!playerManager.AcquiredUserLock(token, character))
            {
                return new ItemBuyResult(ItemTradeState.Failed, new long[0], new decimal[0], 0, 0);
            }

            var possibleMarketItems = gameData.GetMarketItems(itemId);
            var requestAmount = amount;

            var resources = gameData.GetResources(character.ResourcesId);
            var coins = resources.Coins;
            //var coins = character.Resources.Coins;
            var insufficientCoins = false;
            foreach (var marketItem in possibleMarketItems)
            {
                if (requestAmount <= 0)
                {
                    break;
                }

                if (marketItem.PricePerItem > maxPricePerItem)
                {
                    boughtItemCount.Add(0);
                    boughtPricePerItem.Add(marketItem.PricePerItem);
                    continue;
                }

                var buyAmount = 0L;
                if (marketItem.Amount < requestAmount)
                {
                    buyAmount = marketItem.Amount;
                }
                else if (marketItem.Amount >= requestAmount)
                {
                    buyAmount = requestAmount;
                }

                if (buyAmount <= 0 || marketItem.Amount <= 0)
                {
                    if (marketItem.Amount <= 0)
                    {
                        gameData.Remove(marketItem);
                    }
                    else
                    {
                        boughtItemCount.Add(0);
                        boughtPricePerItem.Add(marketItem.PricePerItem);
                    }
                    break;
                }

                var cost = marketItem.PricePerItem * buyAmount;
                if (cost > coins)
                {
                    buyAmount = (long)(coins / marketItem.PricePerItem);
                    // round the cost to what we can afford
                    cost = buyAmount * marketItem.PricePerItem;
                    if (buyAmount <= 0)
                    {
                        insufficientCoins = true;
                    }
                }

                if (buyAmount <= 0)
                {
                    boughtItemCount.Add(0);
                    boughtPricePerItem.Add(marketItem.PricePerItem);
                    continue;
                }

                boughtPricePerItem.Add(marketItem.PricePerItem);
                boughtItemCount.Add(buyAmount);

                boughtTotalCost += cost;
                boughtTotalAmount += buyAmount;

                BuyMarketItemAsync(token, itemId, character, marketItem, buyAmount, cost);

                requestAmount -= buyAmount;
            }

            if (boughtTotalAmount <= 0 || boughtTotalCost <= 0)
            {
                return new ItemBuyResult(
                    boughtItemCount.Count > 0
                        ? boughtItemCount.All(x => x == 0)
                        ? ItemTradeState.InsufficientCoins
                        : ItemTradeState.RequestToLow
                        : ItemTradeState.DoesNotExist,
                    boughtItemCount.ToArray(),
                    boughtPricePerItem.ToArray(), 0, 0);
            }

            character = GetCharacterAsync(token, userId);

            playerManager.EquipBestItems(character);

            return new ItemBuyResult(
                ItemTradeState.Success,
                boughtItemCount.ToArray(),
                boughtPricePerItem.ToArray(),
                boughtTotalAmount,
                boughtTotalCost);
        }

        private void BuyMarketItemAsync(
            SessionToken token,
            Guid itemId,
            Character character,
            DataModels.MarketItem marketItem,
            long amount,
            decimal cost)
        {

            var buyAmount = marketItem.Amount >= amount ? amount : marketItem.Amount;
            if (marketItem.Amount - buyAmount <= 0)
                gameData.Remove(marketItem);
            else
            {
                marketItem.Amount = buyAmount;
            }

            var sellerResources = gameData.GetResourcesByCharacterId(marketItem.SellerCharacterId);
            var buyerResources = gameData.GetResourcesByCharacterId(character.Id);
            //marketItem.SellerCharacter.Resources.Coins += cost;
            sellerResources.Coins += cost;
            buyerResources.Coins -= cost;

            var sellerCharacter = gameData.GetCharacter(marketItem.SellerCharacterId);
            var seller = gameData.GetUser(sellerCharacter.UserId);
            var buyer = gameData.GetUser(character.UserId);

            var inventoryItems = gameData.GetInventoryItems(character.Id, itemId);
            var mergeAmount = buyAmount;
            if (inventoryItems.Count > 0)
            {
                mergeAmount += inventoryItems.Sum(x => x.Amount.GetValueOrDefault());
                gameData.RemoveRange(inventoryItems);
            }

            var mergedInventoryItem = new InventoryItem
            {
                Id = Guid.NewGuid(),
                Amount = mergeAmount,
                CharacterId = character.Id,
                Equipped = false,
                ItemId = itemId,
            };

            gameData.Add(mergedInventoryItem);

            var model = new ItemTradeUpdate
            {
                SellerId = seller?.UserId,
                BuyerId = buyer?.UserId,
                ItemId = itemId,
                Amount = amount,
                Cost = cost
            };

            var sellerSession = gameData.GetUserSession(
                sellerCharacter.UserIdLock.GetValueOrDefault());

            //await db.GameSession
            //    .OrderByDescending(x => x.Started)
            //    .FirstOrDefaultAsync(
            //    x =>
            //        x.UserId == marketItem.SellerCharacter.UserIdLock &&
            //        x.Status == (int)SessionStatus.Active);

            if (sellerSession != null)
            {
                AddGameEvent(sellerSession.Id, GameEventType.ItemSell, model);
            }

            AddGameEvent(token.SessionId, GameEventType.ItemBuy, model);
        }

        private Character GetCharacterAsync(SessionToken token, string userId)
        {
            var user = gameData.GetUser(userId);
            if (user == null) return null;

            return gameData.GetCharacter(user.Id);
        }

        private void AddGameEvent(Guid sessionId, GameEventType type, object model)
        {
            var session = gameData.GetSession(sessionId);
            var gameEvent = gameData.CreateSessionEvent(type, session, model);
            gameData.Add(gameEvent);
        }
    }
}
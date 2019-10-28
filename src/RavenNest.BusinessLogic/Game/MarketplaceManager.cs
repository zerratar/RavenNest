using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using RavenNest.BusinessLogic.Data;
using RavenNest.DataModels;
using RavenNest.Models;
using InventoryItem = RavenNest.DataModels.InventoryItem;

namespace RavenNest.BusinessLogic.Game
{
    public class MarketplaceManager : IMarketplaceManager
    {
        private readonly IGameData gameData;

        public MarketplaceManager(IGameData gameData)
        {
            this.gameData = gameData;
        }

        public async Task<MarketItemCollection> GetItemsAsync(int offset, int size)
        {
            var collection = new MarketItemCollection();
            var marketItemCount = await db.MarketItem.CountAsync();
            var items = await db.MarketItem.Include(x => x.SellerCharacter).ThenInclude(x => x.User).Skip(offset).Take(size).ToListAsync();

            collection.Offset = offset;
            collection.Total = marketItemCount;
            collection.AddRange(
                items.Select(x =>
                {
                    var item = DataMapper.Map<Models.MarketItem, DataModels.MarketItem>(x);
                    item.SellerUserId = x.SellerCharacter.User.UserId;
                    return item;
                }));

            return collection;
        }

        public async Task<ItemSellResult> SellItemAsync(
            SessionToken token, string userId, Guid itemId, long amount, decimal pricePerItem)
        {
            if (amount <= 0 || pricePerItem <= 0)
            {
                return new ItemSellResult(ItemTradeState.RequestToLow);
            }

            var character = await GetCharacterAsync(db, token, userId);
            if (character == null) return new ItemSellResult(ItemTradeState.Failed);

            if (!await PlayerManager.AcquiredUserLockAsync(token, db, character))
            {
                return new ItemSellResult(ItemTradeState.Failed);
            }

            var itemsToSell = character.InventoryItem
                .Where(x => x.ItemId == itemId && !x.Equipped)
                .ToList();


            var totalItemCount = itemsToSell.Count > 0 ? itemsToSell.Sum(x => x.Amount.GetValueOrDefault()) : 0;
            var newItemAmount = totalItemCount - amount;

            if (itemsToSell.Count == 0 || newItemAmount < 0)
            {
                return new ItemSellResult(ItemTradeState.DoesNotOwn);
            }

            db.RemoveRange(itemsToSell);

            if (newItemAmount > 0)
            {
                var mergedInventoryItem = new InventoryItem
                {
                    Id = Guid.NewGuid(),
                    Amount = newItemAmount,
                    CharacterId = character.Id,
                    Equipped = false,
                    ItemId = itemId,
                    Character = character
                };
                await db.InventoryItem.AddAsync(mergedInventoryItem);
            }

            var marketItem = new DataModels.MarketItem
            {
                Id = Guid.NewGuid(),
                Amount = amount,
                Created = DateTime.UtcNow,
                ItemId = itemId,
                PricePerItem = pricePerItem,
                SellerCharacterId = character.Id,
                SellerCharacter = character
            };

            await db.MarketItem.AddAsync(marketItem);
            await db.SaveChangesAsync();

            return new ItemSellResult(ItemTradeState.Success);
        }

        public async Task<ItemBuyResult> BuyItemAsync(
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

            var character = await GetCharacterAsync(db, token, userId);
            if (character == null) return new ItemBuyResult(ItemTradeState.Failed, new long[0], new decimal[0], 0, 0);

            if (!await PlayerManager.AcquiredUserLockAsync(token, db, character))
            {
                return new ItemBuyResult(ItemTradeState.Failed, new long[0], new decimal[0], 0, 0);
            }

            var possibleMarketItems = await db.MarketItem
                .Where(x => x.ItemId == itemId)
                .OrderBy(x => x.PricePerItem)
                .Include(x => x.SellerCharacter)
                .ThenInclude(x => x.Resources)
                .ToListAsync();

            var requestAmount = amount;
            var coins = character.Resources.Coins;
            var updateRequired = false;
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
                        updateRequired = true;
                        db.MarketItem.Remove(marketItem);
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
                        insufficientCoins = true;
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

                await BuyMarketItemAsync(token, itemId, db, character, marketItem, buyAmount, cost);

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

            var character = await GetCharacterAsync(token, userId);
            PlayerManager.EquipBestItems(character);
            await db.SaveChangesAsync();

            return new ItemBuyResult(
                ItemTradeState.Success,
                boughtItemCount.ToArray(),
                boughtPricePerItem.ToArray(),
                boughtTotalAmount,
                boughtTotalCost);
        }

        private async Task BuyMarketItemAsync(
            SessionToken token,
            Guid itemId,
            RavenfallDbContext db,
            Character character,
            DataModels.MarketItem marketItem,
            long amount,
            decimal cost)
        {

            var buyAmount = marketItem.Amount >= amount ? amount : marketItem.Amount;
            if (marketItem.Amount - buyAmount <= 0)
                db.Remove(marketItem);
            else
            {
                marketItem.Amount = buyAmount;
                db.Update(marketItem);
            }

            marketItem.SellerCharacter.Resources.Coins += cost;
            character.Resources.Coins -= cost;

            db.Update(marketItem.SellerCharacter.Resources);
            db.Update(character.Resources);

            var seller = await db.User.FirstOrDefaultAsync(x => x.Id == marketItem.SellerCharacter.UserId);
            var buyer = await db.User.FirstOrDefaultAsync(x => x.Id == character.UserId);

            var inventoryItems =
                await db.InventoryItem
                    .Where(x => x.CharacterId == character.Id && !x.Equipped && x.ItemId == itemId)
                    .ToListAsync();

            var mergeAmount = buyAmount;
            if (inventoryItems.Count > 0)
            {
                mergeAmount += inventoryItems.Sum(x => x.Amount.GetValueOrDefault());
                db.RemoveRange(inventoryItems);
            }

            var mergedInventoryItem = new InventoryItem
            {
                Id = Guid.NewGuid(),
                Amount = mergeAmount,
                CharacterId = character.Id,
                Equipped = false,
                ItemId = itemId,
                Character = character
            };

            await db.InventoryItem.AddAsync(mergedInventoryItem);

            var model = new ItemTradeUpdate
            {
                SellerId = seller?.UserId,
                BuyerId = buyer?.UserId,
                ItemId = itemId,
                Amount = amount,
                Cost = cost
            };

            var sellerSession = await db.GameSession
                    .OrderByDescending(x => x.Started)
                    .FirstOrDefaultAsync(
                    x =>
                        x.UserId == marketItem.SellerCharacter.UserIdLock &&
                        x.Status == (int)SessionStatus.Active);

            if (sellerSession != null)
            {
                await AddGameEventAsync(db, sellerSession.Id, GameEventType.ItemSell, model);
            }

            await AddGameEventAsync(db, token.SessionId, GameEventType.ItemBuy, model);
        }

        private async Task<Character> GetCharacterAsync(SessionToken token, string userId)
        {
            var session = await db.GameSession.FirstOrDefaultAsync(x => x.Id == token.SessionId);
            if (session == null) return null;

            var user = await db.User.FirstOrDefaultAsync(x => x.UserId == userId);
            if (user == null) return null;

            return await db.Character
                .Include(x => x.InventoryItem).ThenInclude(x => x.Item)
                .Include(x => x.Appearance)
                .Include(x => x.Resources)
                .Include(x => x.Skills)
                .Include(x => x.Statistics)
                .FirstOrDefaultAsync(x =>
                    x.UserId == user.Id &&
                    (session.Local && x.Local && x.OriginUserId == session.UserId ||
                     !session.Local && !x.Local));
        }


        private async Task AddGameEventAsync(
            RavenfallDbContext db,
            Guid sessionId,
            GameEventType type,
            object model)
        {

            var events = await db.GameEvent
                .Where(x => x.GameSessionId == sessionId)
                .ToListAsync();

            var revision = 1 + events.Count;

            await db.GameEvent.AddAsync(new DataModels.GameEvent
            {
                Id = Guid.NewGuid(),
                GameSessionId = sessionId,
                Type = (int)type,
                Data = JSON.Stringify(model),
                Revision = revision
            });
        }
    }
}
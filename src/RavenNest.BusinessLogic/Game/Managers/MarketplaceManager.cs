using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Extensions;
using RavenNest.BusinessLogic.Providers;
using RavenNest.DataModels;
using RavenNest.Models;

namespace RavenNest.BusinessLogic.Game
{
    public class MarketplaceManager
    {
        private readonly ILogger<MarketplaceManager> logger;
        private readonly PlayerManager playerManager;
        private readonly PlayerInventoryProvider inventoryProvider;
        private readonly GameData gameData;

        public MarketplaceManager(
            ILogger<MarketplaceManager> logger,
            PlayerManager playerManager,
            PlayerInventoryProvider inventoryProvider,
            GameData gameData)
        {
            this.logger = logger;
            this.playerManager = playerManager;
            this.inventoryProvider = inventoryProvider;
            this.gameData = gameData;
        }

        public MarketItemCollection GetItems(ItemFilter filter, int offset, int size)
        {
            try
            {
                var collection = new MarketItemCollection();
                var marketItemCount = gameData.GetMarketItemCount(filter);
                var items = gameData.GetMarketItems(filter, offset, size);

                collection.Offset = offset;
                collection.Total = marketItemCount;
                collection.AddRange(
                    items.Select(x =>
                    {
                        var character = gameData.GetCharacter(x.SellerCharacterId);
                        if (character == null) return null;
                        var user = gameData.GetUser(character.UserId);
                        if (user == null) return null;
                        var item = DataMapper.Map<RavenNest.Models.MarketItem, DataModels.MarketItem>(x);
                        item.SellerUserId = user.Id;
                        return item;
                    })
                    .Where(x => x != null));

                return collection;
            }
            catch (Exception exc)
            {
                logger.LogError(exc.ToString());
                return null;
            }
        }
        public MarketItemCollection GetItems(int offset, int size)
        {
            try
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
                        if (character == null) return null;
                        var user = gameData.GetUser(character.UserId);
                        if (user == null) return null;
                        var item = DataMapper.Map<RavenNest.Models.MarketItem, DataModels.MarketItem>(x);
                        item.SellerUserId = user.Id;
                        return item;
                    })
                    .Where(x => x != null));

                return collection;
            }
            catch (Exception exc)
            {
                logger.LogError(exc.ToString());
                return null;
            }
        }

        public bool Cancel(Guid id)
        {
            var i = gameData.GetMarketItem(id);
            if (i == null) return false;

            // Add items back to the character.
            return playerManager.ReturnMarketplaceItem(i);
        }

        public ItemSellResult SellItem(SessionToken token, string userId, string platform, Guid itemId, long amount, double pricePerItem)
        {
            var character = GetCharacter(token, userId, platform);
            if (character == null) return new ItemSellResult(ItemTradeState.Failed);
            return SellItem(token, itemId, amount, pricePerItem, character);
        }

        public ItemSellResult SellItem(SessionToken token, Guid characterId, Guid itemId, long amount, double pricePerItem)
        {
            var character = GetCharacter(token, characterId);
            if (character == null) return new ItemSellResult(ItemTradeState.Failed);
            return SellItem(token, itemId, amount, pricePerItem, character);
        }

        private ItemSellResult SellItem(SessionToken token, Guid itemId, long amount, double pricePerItem, Character character)
        {
            if (amount <= 0 || pricePerItem <= 0)
            {
                return new ItemSellResult(ItemTradeState.RequestToLow);
            }

            if (pricePerItem >= 1_000_000_000)
            {
                return new ItemSellResult(ItemTradeState.Failed);
            }

            if (!playerManager.AcquiredUserLock(token, character))
            {
                return new ItemSellResult(ItemTradeState.Failed);
            }

            var inventory = inventoryProvider.Get(character.Id);
            var session = gameData.GetSession(token.SessionId);
            var sessionOwner = gameData.GetUser(session.UserId);
            var item = gameData.GetItem(itemId);
            string itemTag = null;

            if (item.Soulbound)
                return new ItemSellResult(ItemTradeState.Untradable);

            if (item.Category == (int)DataModels.ItemCategory.StreamerToken)
                itemTag = sessionOwner.UserId;

            var itemToSell = inventory.GetUnequippedItem(itemId, tag: itemTag);

            if (itemToSell.IsNull())
            {
                return new ItemSellResult(ItemTradeState.DoesNotOwn);
            }

            if (!string.IsNullOrEmpty(itemToSell.Enchantment))
            {
                return new ItemSellResult(ItemTradeState.Untradable);
            }

            var totalItemCount = itemToSell.Amount;//itemsToSell.Count > 0 ? itemsToSell.Sum(x => x.Amount.GetValueOrDefault()) : 0;
            var newItemAmount = totalItemCount - amount;

            if (newItemAmount < 0)
            {
                return new ItemSellResult(ItemTradeState.DoesNotOwn);
            }

            inventory.RemoveItem(itemToSell, amount);

            var marketItem = new DataModels.MarketItem
            {
                Id = Guid.NewGuid(),
                Amount = amount,
                Created = DateTime.UtcNow,
                ItemId = itemId,
                PricePerItem = pricePerItem,
                SellerCharacterId = character.Id,
                Tag = itemToSell.Tag,
                Flags = itemToSell.Flags,
                Enchantment = itemToSell.Enchantment,
                Name = itemToSell.Name,
                TransmogrificationId = itemToSell.TransmogrificationId,
                Expires = DateTime.UtcNow.AddDays(14),
            };

            gameData.Add(marketItem);

            return new ItemSellResult(ItemTradeState.Success);
        }

        public ItemValueResult GetItemValue(Guid itemId, long amount)
        {
            var item = gameData.GetItem(itemId);
            if (item == null) return new ItemValueResult();

            var marketItems = gameData.GetMarketItems(itemId);
            if (marketItems == null || marketItems.Count <= 0)
            {
                return new ItemValueResult();
            }

            var cheapest = marketItems.Min(x => x.PricePerItem);
            var mostExpensive = marketItems.Max(x => x.PricePerItem);
            var average = marketItems.Average(x => x.PricePerItem);
            return new ItemValueResult(cheapest, mostExpensive, average, marketItems.Sum(x => x.Amount),
                amount <= 1 ? cheapest : GetCheapestPrice(marketItems, amount));
        }

        private double GetCheapestPrice(IReadOnlyList<DataModels.MarketItem> marketItems, long amount)
        {
            var amountLeft = amount;
            var price = 0d;
            foreach (var stack in marketItems.OrderBy(x => x.PricePerItem))
            {
                if (amountLeft <= stack.Amount)
                {
                    price += (stack.PricePerItem * amountLeft);
                    break;
                }
                else
                {
                    price += (stack.PricePerItem * stack.Amount);
                    amountLeft -= stack.Amount;
                }
            }
            return price;
        }

        public ItemBuyResult BuyItem(SessionToken token, string userId, string platform, Guid itemId, long amount, double maxPricePerItem)
        {
            var character = GetCharacter(token, userId, platform);
            if (character == null) return new ItemBuyResult(ItemTradeState.Failed, Array.Empty<long>(), Array.Empty<double>(), 0, 0);
            return BuyItem(token, itemId, amount, maxPricePerItem, character);
        }

        public ItemBuyResult BuyItem(SessionToken token, Guid characterId, Guid itemId, long amount, double maxPricePerItem)
        {
            var character = GetCharacter(token, characterId);
            if (character == null) return new ItemBuyResult(ItemTradeState.Failed, Array.Empty<long>(), Array.Empty<double>(), 0, 0);
            return BuyItem(token, itemId, amount, maxPricePerItem, character);
        }

        private ItemBuyResult BuyItem(SessionToken token, Guid itemId, long amount, double maxPricePerItem, Character character)
        {
            // todo(zerratar): Rewrite this!! This is horrible
            // The idea behind the following logic:
            //  Player should be able to buy items from multiple sellers
            //  as long as its satisfies their max price per item, trying to buy the
            //  order by the cheapest items. 
            // <<< This is stupid >>>

            // What it should do: (other than being cleaner..)
            // Get the cheapest items that it can, if it can fullfill the amount then great!
            // If it can't fulfill the amount, make sure we take items from other stacks even if
            // they goes beyond the maxPricePerItem, but as long as the AVERAGE price does
            // not exceed the maxPricePer item, it is fine to pick those items too.

            var boughtPricePerItem = new List<double>();
            var boughtItemCount = new List<long>();
            var boughtTotalCost = 0d;
            var boughtTotalAmount = 0L;

            if (amount <= 0 || maxPricePerItem <= 0)
            {
                return new ItemBuyResult(ItemTradeState.RequestToLow, Array.Empty<long>(), Array.Empty<double>(), 0, 0);
            }

            if (!playerManager.AcquiredUserLock(token, character))
            {
                return new ItemBuyResult(ItemTradeState.Failed, Array.Empty<long>(), Array.Empty<double>(), 0, 0);
            }

            var session = gameData.GetSession(token.SessionId);
            var sessionOwner = gameData.GetUser(session.UserId);
            var item = gameData.GetItem(itemId);
            string itemTag = null;
            if (item.Category == (int)DataModels.ItemCategory.StreamerToken)
                itemTag = sessionOwner.UserId;

            var possibleMarketItems = gameData.GetMarketItems(itemId, itemTag);
            var requestAmount = amount;

            var resources = gameData.GetResources(character);
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
                else
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

                var ba = BuyMarketItem(token, itemId, character, marketItem, buyAmount, marketItem.PricePerItem);
                requestAmount -= ba;

                boughtPricePerItem.Add(marketItem.PricePerItem);
                boughtItemCount.Add(ba);

                boughtTotalCost += (ba * marketItem.PricePerItem);
                boughtTotalAmount += ba;
            }

            if (boughtTotalAmount <= 0 || boughtTotalCost <= 0)
            {
                return new ItemBuyResult(
                    boughtItemCount.Count > 0
                        ? boughtItemCount.All(x => x == 0) || insufficientCoins
                        ? ItemTradeState.InsufficientCoins
                        : ItemTradeState.RequestToLow
                        : ItemTradeState.DoesNotExist,
                    boughtItemCount.ToArray(),
                    boughtPricePerItem.ToArray(), 0, 0);
            }

            var inventory = inventoryProvider.Get(character.Id);
            //inventory.EquipBestItems();

            return new ItemBuyResult(
                ItemTradeState.Success,
                boughtItemCount.ToArray(),
                boughtPricePerItem.ToArray(),
                boughtTotalAmount,
                boughtTotalCost);
        }

        private int BuyMarketItem(
            SessionToken token,
            Guid itemId,
            Character character,
            DataModels.MarketItem marketItem,
            long amount,
            double pricePerItem)
        {
            // todo(zerratar): Rewrite this!! This is horrible
            var buyer = gameData.GetUser(character.UserId);
            var buyAmount = marketItem.Amount >= amount ? amount : marketItem.Amount;
            var buyerResources = gameData.GetResources(buyer);
            var totalCost = buyAmount * pricePerItem;
            if (totalCost > buyerResources.Coins)
            {
                return 0;
            }

            if (marketItem.Amount == buyAmount)
                gameData.Remove(marketItem);
            else
                marketItem.Amount -= buyAmount;

            var sellerCharacter = gameData.GetCharacter(marketItem.SellerCharacterId);
            var seller = gameData.GetUser(sellerCharacter.UserId);

            var sellerResources = gameData.GetResources(seller);
            sellerResources.Coins += totalCost;
            buyerResources.Coins -= totalCost;

            var inventory = inventoryProvider.Get(character.Id);
            inventory.AddItem(itemId, buyAmount, tag: marketItem.Tag, equipped: false,
                enchantment: marketItem.Enchantment, name: marketItem.Name,
                transmogrificationId: marketItem.TransmogrificationId,
                flags: marketItem.Flags);

            gameData.Add(
                new MarketItemTransaction
                {
                    Id = Guid.NewGuid(),
                    Amount = buyAmount,
                    BuyerCharacterId = character.Id,
                    SellerCharacterId = sellerCharacter.Id,
                    ItemId = itemId,
                    PricePerItem = (totalCost / buyAmount),
                    TotalPrice = totalCost,
                    Created = DateTime.UtcNow
                });

            var model = new ItemTradeUpdate
            {
                SellerPlayerId = sellerCharacter.Id,
                BuyerPlayerId = character.Id,
                ItemId = itemId,
                Amount = buyAmount,//amount,
                Cost = totalCost
            };

            var sellerSession = gameData.GetSessionByUserId(
                sellerCharacter.UserIdLock.GetValueOrDefault());

            if (sellerSession != null)
            {
                AddGameEvent(sellerSession.Id, GameEventType.ItemSell, model);
            }

            AddGameEvent(token.SessionId, GameEventType.ItemBuy, model);

            return (int)buyAmount;
        }

        private Character GetCharacter(SessionToken token, string userId, string platform)
        {
            var user = gameData.GetUser(userId, platform);
            if (user == null) return null;
            var session = gameData.GetSession(token.SessionId);
            if (session == null) return null;
            var chars = gameData.GetActiveSessionCharacters(session);
            return chars.FirstOrDefault(x => x.UserId == user.Id);
        }

        private Character GetCharacter(SessionToken token, Guid characterId)
        {
            var session = gameData.GetSession(token.SessionId);
            if (session == null) return null;
            var chars = gameData.GetActiveSessionCharacters(session);
            return chars.FirstOrDefault(x => x.Id == characterId);
        }

        private void AddGameEvent(Guid sessionId, GameEventType type, object model)
        {
            var session = gameData.GetSession(sessionId);
            var gameEvent = gameData.CreateSessionEvent(type, session, model);
            gameData.EnqueueGameEvent(gameEvent);
        }

    }
}

﻿using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Providers;
using RavenNest.DataModels;
using RavenNest.Models;

namespace RavenNest.BusinessLogic.Game
{
    public class MarketplaceManager : IMarketplaceManager
    {
        private readonly ILogger<MarketplaceManager> logger;
        private readonly IPlayerManager playerManager;
        private readonly IPlayerInventoryProvider inventoryProvider;
        private readonly IGameData gameData;

        public MarketplaceManager(
            ILogger<MarketplaceManager> logger,
            IPlayerManager playerManager,
            IPlayerInventoryProvider inventoryProvider,
            IGameData gameData)
        {
            this.logger = logger;
            this.playerManager = playerManager;
            this.inventoryProvider = inventoryProvider;
            this.gameData = gameData;
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
                        var item = DataMapper.Map<Models.MarketItem, DataModels.MarketItem>(x);
                        item.SellerUserId = user.UserId;
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

        public ItemSellResult SellItem(
            SessionToken token, string userId, Guid itemId, long amount, decimal pricePerItem)
        {
            //if (i != null && i.Category == (int)DataModels.ItemCategory.StreamerToken)
            //{
            //    return new ItemSellResult(ItemTradeState.Failed);
            //}

            if (amount <= 0 || pricePerItem <= 0)
            {
                return new ItemSellResult(ItemTradeState.RequestToLow);
            }

            if (pricePerItem >= 1_000_000_000)
            {
                return new ItemSellResult(ItemTradeState.Failed);
            }

            var character = GetCharacterAsync(token, userId);
            if (character == null) return new ItemSellResult(ItemTradeState.Failed);

            if (!playerManager.AcquiredUserLock(token, character))
            {
                return new ItemSellResult(ItemTradeState.Failed);
            }

            var inventory = inventoryProvider.Get(character.Id);
            var session = gameData.GetSession(token.SessionId);
            var sessionOwner = gameData.GetUser(session.UserId);
            var item = gameData.GetItem(itemId);
            string itemTag = null;

            if (item.Soulbound.GetValueOrDefault())
                return new ItemSellResult(ItemTradeState.Failed);

            if (item.Category == (int)DataModels.ItemCategory.StreamerToken)
                itemTag = sessionOwner.UserId;

            var itemToSell = inventory.GetUnequippedItem(itemId, tag: itemTag);

            if (itemToSell.IsNull())
            {
                return new ItemSellResult(ItemTradeState.DoesNotOwn);
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
                Tag = itemToSell.Tag
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

            var session = gameData.GetSession(token.SessionId);
            var sessionOwner = gameData.GetUser(session.UserId);
            var item = gameData.GetItem(itemId);
            string itemTag = null;
            if (item.Category == (int)DataModels.ItemCategory.StreamerToken)
                itemTag = sessionOwner.UserId;


            var possibleMarketItems = gameData.GetMarketItems(itemId, itemTag);
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

                boughtPricePerItem.Add(marketItem.PricePerItem);
                boughtItemCount.Add(buyAmount);

                boughtTotalCost += cost;
                boughtTotalAmount += buyAmount;

                requestAmount -= BuyMarketItemAsync(token, itemId, character, marketItem, buyAmount, cost);
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


            if (boughtItemCount.Count > 1)
            {


            }
            else
            {

            }

            return new ItemBuyResult(
                ItemTradeState.Success,
                boughtItemCount.ToArray(),
                boughtPricePerItem.ToArray(),
                boughtTotalAmount,
                boughtTotalCost);
        }

        private int BuyMarketItemAsync(
            SessionToken token,
            Guid itemId,
            Character character,
            DataModels.MarketItem marketItem,
            long amount,
            decimal cost)
        {
            var buyAmount = marketItem.Amount >= amount ? amount : marketItem.Amount;
            if (marketItem.Amount == buyAmount)
                gameData.Remove(marketItem);
            else
                marketItem.Amount -= buyAmount;

            var sellerResources = gameData.GetResourcesByCharacterId(marketItem.SellerCharacterId);
            var buyerResources = gameData.GetResourcesByCharacterId(character.Id);
            sellerResources.Coins += cost;
            buyerResources.Coins -= cost;

            var sellerCharacter = gameData.GetCharacter(marketItem.SellerCharacterId);
            var seller = gameData.GetUser(sellerCharacter.UserId);
            var buyer = gameData.GetUser(character.UserId);


            var inventory = inventoryProvider.Get(character.Id);
            inventory.AddItem(itemId, buyAmount, tag: marketItem.Tag);

            gameData.Add(
                new MarketItemTransaction
                {
                    Id = Guid.NewGuid(),
                    Amount = buyAmount,
                    BuyerCharacterId = character.Id,
                    SellerCharacterId = sellerCharacter.Id,
                    ItemId = itemId,
                    PricePerItem = (cost / buyAmount),
                    TotalPrice = cost,
                    Created = DateTime.UtcNow
                });

            var model = new ItemTradeUpdate
            {
                SellerId = seller?.UserId,
                BuyerId = buyer?.UserId,
                ItemId = itemId,
                Amount = buyAmount,//amount,
                Cost = cost
            };

            var sellerSession = gameData.GetUserSession(
                sellerCharacter.UserIdLock.GetValueOrDefault());

            if (sellerSession != null)
            {
                AddGameEvent(sellerSession.Id, GameEventType.ItemSell, model);
            }

            AddGameEvent(token.SessionId, GameEventType.ItemBuy, model);

            return (int)buyAmount;
        }

        private Character GetCharacterAsync(SessionToken token, string userId)
        {
            var user = gameData.GetUser(userId);
            if (user == null) return null;

            var session = gameData.GetSession(token.SessionId);
            if (session == null)
            {
                return null;
            }

            var chars = gameData.GetSessionCharacters(session);
            return chars.FirstOrDefault(x => x.UserId == user.Id);
        }

        private void AddGameEvent(Guid sessionId, GameEventType type, object model)
        {
            var session = gameData.GetSession(sessionId);
            var gameEvent = gameData.CreateSessionEvent(type, session, model);
            gameData.Add(gameEvent);
        }
    }
}

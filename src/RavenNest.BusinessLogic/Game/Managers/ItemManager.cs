using System;
using System.Linq;
using Microsoft.Extensions.Caching.Memory;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Extensions;
using RavenNest.Models;

namespace RavenNest.BusinessLogic.Game
{
    public sealed class ItemManager
    {
        private const double ItemCacheDurationSeconds = 10 * 60;
        private readonly IMemoryCache memoryCache;
        private readonly GameData gameData;

        private DateTime lastCacheInvalidation;

        public ItemManager(
            IMemoryCache memoryCache,
            GameData gameData)
        {
            this.memoryCache = memoryCache;
            this.gameData = gameData;
        }

        public RedeemableItemCollection GetRedeemableItems()
        {
            return new RedeemableItemCollection(gameData
                .GetRedeemableItems()
                .Select(x => DataMapper.Map<RedeemableItem, DataModels.RedeemableItem>(x)));
        }

        public ResourceItemDropCollection GetResourceItemDrops()
        {
            return new ResourceItemDropCollection(gameData
                .GetResourceItemDrops()
                .Select(x => ModelMapper.Map(x)));
        }

        public ItemRecipeCollection GetRecipes()
        {
            if (memoryCache.TryGetValue<ItemRecipeCollection>("GetRecipes", out var recipes))
            {
                return recipes;
            }

            var collection = new ItemRecipeCollection(gameData.GetItemRecipes().Select(x => ModelMapper.Map(gameData, x)));
            return memoryCache.Set("GetRecipes", collection, DateTime.UtcNow.AddSeconds(ItemCacheDurationSeconds));
        }

        public ItemCollection GetAllItems()
        {
            if (memoryCache.TryGetValue<ItemCollection>("GetAllItems", out var itemCollection))
            {
                return itemCollection;
            }

            return InvalidateCache();
        }

        public ItemCollection GetAllItems(DateTime timestamp)
        {
            var items = gameData.GetItems();
            var collection = new ItemCollection();
            foreach (var item in items)
            {
                if (item.Hidden || item.Modified.GetValueOrDefault() < timestamp)
                {
                    continue;
                }

                collection.Add(ModelMapper.Map(gameData, item));
            }

            if (timestamp > lastCacheInvalidation && collection.Count > 0)
            {
                // force update the cache as we have new items
                InvalidateCache();
            }

            return collection;
        }

        public Item GetItem(Guid itemId)
        {
            var dataItem = gameData.GetItem(itemId);
            return ModelMapper.Map(gameData, dataItem);
        }

        public bool Upsert(Item item)
        {
            try
            {
                DataModels.Item dataItem = GetItem(item);
                if (dataItem == null)
                {
                    AddItem(item);
                    return true;
                }

                UpdateItem(item, dataItem);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool TryAddItem(Item item)
        {
            var dataItem = gameData.GetItem(item.Id);
            if (dataItem == null)
            {
                AddItem(item);
                return true;
            }

            return false;
        }

        private void AddItem(Item item)
        {
            var entity = ModelMapper.Map(item);

            entity.Modified = DateTime.UtcNow;

            gameData.Add(entity);
            InvalidateCache();
        }

        private static DataModels.ItemCraftingRequirement Map(ItemCraftingRequirement req)
        {
            return new DataModels.ItemCraftingRequirement
            {
                Id = req.Id == Guid.Empty ? Guid.NewGuid() : req.Id,
                Amount = req.Amount,
                ItemId = req.ItemId,
                ResourceItemId = req.ResourceItemId,
            };
        }

        public bool TryUpdateItem(Item item)
        {
            DataModels.Item dataItem = GetItem(item);
            if (dataItem == null)
            {
                return false;
            }

            UpdateItem(item, dataItem);
            return true;
        }

        private void UpdateItem(Item item, DataModels.Item dataItem)
        {
            dataItem.Description = item.Description;

            dataItem.Level = item.Level;
            dataItem.ArmorPower = item.ArmorPower;
            dataItem.WeaponAim = item.WeaponAim;
            dataItem.WeaponPower = item.WeaponPower;
            dataItem.MagicPower = item.MagicPower;
            dataItem.MagicAim = item.MagicAim;
            dataItem.RangedPower = item.RangedPower;
            dataItem.RangedAim = item.RangedAim;

            dataItem.Category = (int)item.Category;
            dataItem.FemaleModelId = item.FemaleModelId;
            dataItem.FemalePrefab = item.FemalePrefab;
            dataItem.GenericPrefab = item.GenericPrefab;
            dataItem.MaleModelId = item.MaleModelId;
            dataItem.MalePrefab = item.MalePrefab;
            dataItem.Material = (int)item.Material;
            dataItem.Name = item.Name;
            dataItem.RangedAim = item.RangedAim;
            dataItem.RangedPower = item.RangedPower;
            dataItem.RequiredAttackLevel = item.RequiredAttackLevel;
            dataItem.RequiredDefenseLevel = item.RequiredDefenseLevel;
            dataItem.RequiredMagicLevel = item.RequiredMagicLevel;
            dataItem.RequiredRangedLevel = item.RequiredRangedLevel;
            dataItem.RequiredSlayerLevel = item.RequiredSlayerLevel;
            dataItem.ShopBuyPrice = item.ShopBuyPrice;
            dataItem.ShopSellPrice = item.ShopSellPrice;
            dataItem.Soulbound = item.Soulbound;
            dataItem.Type = (int)item.Type;
            dataItem.Modified = DateTime.UtcNow;

            InvalidateCache();
        }

        private DataModels.Item GetItem(Item item)
        {
            DataModels.Item dataItem = null;
            if (item.Id == Guid.Empty)
            {
                dataItem = gameData.GetItems().FirstOrDefault(x => x.Name.Equals(item.Name, StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                dataItem = gameData.GetItem(item.Id);
            }

            return dataItem;
        }

        public bool RemoveItem(Guid itemId)
        {
            var dataItem = gameData.GetItem(itemId);
            if (dataItem == null)
            {
                return false;
            }

            // remove all inventory items using this item
            // remove all inventory item attributes using this item
            // remove all stash using this item
            // remove all item drops using this item
            // remove all crafting requirements using this item
            // remove all market items
            // remove all vendor items
            // remove all redeemable items
            // remove all market transactions
            // remove all vendor transactions
            // remove all loyalty reward
            //gameData.Remove(dataItem);

            return false;
        }

        private ItemCollection InvalidateCache()
        {
            lastCacheInvalidation = DateTime.UtcNow;
            var items = gameData.GetItems();
            var collection = new ItemCollection();
            foreach (var item in items)
            {
                if (item.Hidden)
                {
                    continue;
                }

                collection.Add(ModelMapper.Map(gameData, item));
            }

            return memoryCache.Set("GetAllItems", collection, DateTime.UtcNow.AddSeconds(ItemCacheDurationSeconds));
        }
    }
}

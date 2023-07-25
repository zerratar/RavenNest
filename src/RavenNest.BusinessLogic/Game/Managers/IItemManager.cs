using System;
using System.Linq;
using Microsoft.Extensions.Caching.Memory;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Extensions;
using RavenNest.Models;

namespace RavenNest.BusinessLogic.Game
{
    public interface IItemManager
    {
        bool Upsert(Item item);
        ItemCollection GetAllItems();
        Item GetItem(Guid itemId);
        bool TryAddItem(Item item);
        bool TryUpdateItem(Item item);
        bool RemoveItem(Guid itemId);
        RedeemableItemCollection GetRedeemableItems();
    }

    public class ItemManager : IItemManager
    {
        private const double ItemCacheDurationSeconds = 10 * 60;
        private readonly IMemoryCache memoryCache;

        private readonly GameData gameData;

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
                .Select(x => DataMapper.Map<RavenNest.Models.RedeemableItem, DataModels.RedeemableItem>(x)));
        }

        public ItemCollection GetAllItems()
        {
            if (memoryCache.TryGetValue<ItemCollection>("GetAllItems", out var itemCollection))
            {
                return itemCollection;
            }

            return InvalidateCache();
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
            gameData.Add(entity);
            InvalidateCache();
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
            dataItem.Level = item.Level;
            dataItem.ArmorPower = item.ArmorPower;
            dataItem.Category = (int)item.Category;
            dataItem.Craftable = item.Craftable;
            dataItem.FemaleModelId = item.FemaleModelId;
            dataItem.FemalePrefab = item.FemalePrefab;
            dataItem.GenericPrefab = item.GenericPrefab;
            dataItem.MaleModelId = item.MaleModelId;
            dataItem.MalePrefab = item.MalePrefab;
            dataItem.Material = (int)item.Material;
            dataItem.Name = item.Name;
            dataItem.OreCost = item.OreCost;
            dataItem.RangedAim = item.RangedAim;
            dataItem.RangedPower = item.RangedPower;
            dataItem.RequiredAttackLevel = item.RequiredAttackLevel;
            dataItem.RequiredCraftingLevel = item.RequiredCraftingLevel;
            dataItem.RequiredDefenseLevel = item.RequiredDefenseLevel;
            dataItem.RequiredMagicLevel = item.RequiredMagicLevel;
            dataItem.RequiredRangedLevel = item.RequiredRangedLevel;
            dataItem.RequiredSlayerLevel = item.RequiredSlayerLevel;
            dataItem.ShopBuyPrice = item.ShopBuyPrice;
            dataItem.ShopSellPrice = item.ShopSellPrice;
            dataItem.Soulbound = item.Soulbound;
            dataItem.Type = (int)item.Type;
            dataItem.WeaponAim = item.WeaponAim;
            dataItem.WeaponPower = item.WeaponPower;
            dataItem.WoodCost = item.WoodCost;
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

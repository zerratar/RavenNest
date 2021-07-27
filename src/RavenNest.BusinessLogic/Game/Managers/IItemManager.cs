using System;
using Microsoft.Extensions.Caching.Memory;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Extensions;
using RavenNest.Models;

namespace RavenNest.BusinessLogic.Game
{
    public interface IItemManager
    {
        ItemCollection GetAllItems();
        Item GetItem(Guid itemId);
        bool AddItem(Item item);
        bool UpdateItem(Item item);
        bool RemoveItem(Guid itemId);
    }

    public class ItemManager : IItemManager
    {
        private const double ItemCacheDurationSeconds = 10 * 60;
        private readonly IMemoryCache memoryCache;

        private readonly IGameData gameData;

        public ItemManager(
            IMemoryCache memoryCache,
            IGameData gameData)
        {
            this.memoryCache = memoryCache;
            this.gameData = gameData;
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

        public bool AddItem(Item item)
        {
            var dataItem = gameData.GetItem(item.Id);
            if (dataItem == null)
            {
                var entity = ModelMapper.Map(item);
                gameData.Add(entity);
                InvalidateCache();
                return true;
            }

            return false;
        }

        public bool UpdateItem(Item item)
        {
            var dataItem = gameData.GetItem(item.Id);
            if (dataItem == null)
            {
                return false;
            }

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

            return true;
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

            //gameData.Remove(dataItem);

            return false;
        }

        private ItemCollection InvalidateCache()
        {
            var items = gameData.GetItems();
            var collection = new ItemCollection();
            foreach (var item in items)
            {
                collection.Add(ModelMapper.Map(gameData, item));
            }

            return memoryCache.Set("GetAllItems", collection, DateTime.UtcNow.AddSeconds(ItemCacheDurationSeconds));
        }
    }
}

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

            var items = gameData.GetItems();
            var collection = new ItemCollection();
            foreach (var item in items)
            {
                collection.Add(ModelMapper.Map(item));
            }

            return memoryCache.Set("GetAllItems", collection, DateTime.UtcNow.AddSeconds(ItemCacheDurationSeconds));
        }

        public bool AddItem(Item item)
        {
            var dataItem = gameData.GetItem(item.Id);
            if (dataItem == null)
            {
                var entity = ModelMapper.Map(item);
                gameData.Add(entity);
                return true;
            }

            return false;
        }

        public bool UpdateItem(Item item)
        {
            return false;
        }

        public bool RemoveItem(Guid itemId)
        {
            return false;
        }
    }
}
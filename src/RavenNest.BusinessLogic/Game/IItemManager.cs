using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Extensions;
using RavenNest.Models;

namespace RavenNest.BusinessLogic.Game
{
    public interface IItemManager
    {
        ItemCollection GetAllItems(AuthToken token);
        bool AddItem(AuthToken token, Item item);
        bool UpdateItem(AuthToken token, Item item);
        bool RemoveItem(AuthToken token, Guid itemId);
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

        public ItemCollection GetAllItems(AuthToken token)
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

        public bool AddItem(AuthToken token, Item item)
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

        public bool UpdateItem(AuthToken token, Item item)
        {
            return false;
        }

        public bool RemoveItem(AuthToken token, Guid itemId)
        {
            return false;
        }
    }
}
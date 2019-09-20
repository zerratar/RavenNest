using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Extensions;
using RavenNest.Models;

namespace RavenNest.BusinessLogic.Game
{
    public interface IItemManager
    {
        ItemCollection GetAllItems(AuthToken token);
        Task<bool> AddItemAsync(AuthToken token, Item item);
        bool UpdateItem(AuthToken token, Item item);
        bool RemoveItem(AuthToken token, Guid itemId);
    }

    public class ItemManager : IItemManager
    {
        private readonly IRavenfallDbContextProvider dbProvider;

        public ItemManager(IRavenfallDbContextProvider dbProvider)
        {
            this.dbProvider = dbProvider;
        }

        public ItemCollection GetAllItems(AuthToken token)
        {
            using (var db = this.dbProvider.Get())
            {
                var items = db.Item.ToList();
                var collection = new ItemCollection();

                foreach (var item in items)
                {
                    collection.Add(ModelMapper.Map(item));
                }

                return collection;
            }
        }

        public async Task<bool> AddItemAsync(AuthToken token, Item item)
        {
            using (var db = dbProvider.Get())
            {
                var entity = ModelMapper.Map(item);
                if (await db.Item.FirstOrDefaultAsync(x => x.Id == item.Id) == null)
                {
                    await db.Item.AddAsync(entity);
                    await db.SaveChangesAsync();
                    return true;
                }

                return false;
            }
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
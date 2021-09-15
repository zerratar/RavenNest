using Microsoft.AspNetCore.Http;
using RavenNest.BusinessLogic.Game;
using RavenNest.Models;
using RavenNest.Sessions;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;

namespace RavenNest.Blazor.Services
{
    public class ItemService : RavenNestService
    {
        private readonly IItemManager itemManager;
        public ItemService(IItemManager itemManager, IHttpContextAccessor accessor, ISessionInfoProvider sessionInfoProvider)
            : base(accessor, sessionInfoProvider)
        {
            this.itemManager = itemManager;
        }

        public Item GetItem(Guid itemId)
        {
            return itemManager.GetItem(itemId);
        }

        public ItemCollection GetItems()
        {
            return itemManager.GetAllItems();
        }

        public Task<IEnumerable<Item>> SearchAsync(string search)
        {
            return Task.Run(() =>
            {
                var items = itemManager.GetAllItems();
                return items.Where(x => x.Name.Contains(search, StringComparison.OrdinalIgnoreCase));
            });
        }
        public async Task<ItemCollection> GetItemsAsync()
        {
            return await Task.Run(() => itemManager.GetAllItems());
        }

        public async Task<bool> AddOrUpdateItemAsync(Item item)
        {
            return await Task.Run(() =>
            {
                var session = GetSession();
                if (!session.Authenticated || !session.Administrator)
                    return false;


                return itemManager.Upsert(item);

                //var existing = itemManager.GetItem(item.Id);
                //if (existing != null)
                //{
                //    return itemManager.TryUpdateItem(item);
                //}
                //return itemManager.TryAddItem(item);

            }).ConfigureAwait(false);
        }
    }
}

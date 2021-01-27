using RavenNest.BusinessLogic.Game;
using RavenNest.Models;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;

namespace RavenNest.Blazor.Services
{
    public class ItemService
    {
        private readonly IItemManager itemManager;
        public ItemService(IItemManager itemManager)
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
    }
}

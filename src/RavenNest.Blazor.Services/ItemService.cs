using RavenNest.BusinessLogic.Game;
using RavenNest.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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

        public ItemCollection GetItems()
        {
            return itemManager.GetAllItems();
        }

        public async Task<ItemCollection> GetItemsAsync()
        {
            return await Task.Run(() => itemManager.GetAllItems());
        }
    }
}

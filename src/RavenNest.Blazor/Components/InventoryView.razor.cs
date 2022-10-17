using Microsoft.AspNetCore.Components;
using RavenNest.Blazor.Services;
using RavenNest.BusinessLogic.Extended;
using RavenNest.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RavenNest.Blazor.Components
{
    public partial class InventoryView : ComponentBase
    {
        [Parameter] public WebsiteAdminUser SelectedUser { get; set; }
        [Inject] public ItemService ItemService { get; set; }

        public List<ItemInstance> ItemInstances = new();

        protected override async Task OnParametersSetAsync()
        {
            await base.OnParametersSetAsync();

            if (ItemInstances.Count > 0)
                ItemInstances.Clear();

            var stash = SelectedUser.Stash;
            foreach (var item in stash)
            {
                ItemInstances.Add(new ItemInstance(item, ItemService.GetItemEquipmentSlot(item.ItemId)));
            }
            foreach (var character in SelectedUser.Characters)
            {

                foreach (var item in character.InventoryItems)
                {
                    ItemInstances.Add(new ItemInstance(item, ItemService.GetItemEquipmentSlot(item.ItemId)));
                }
            }
            return;
        }

        void HandleItemUpdate(ItemInstance itemInstance)
        {
            //TODO - popup whenever an item has been successfully updated
        }
    }
}

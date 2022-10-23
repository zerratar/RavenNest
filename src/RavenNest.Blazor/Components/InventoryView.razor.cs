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
        [Parameter] public bool CanManage { get; set; }
        [Inject] public ItemService ItemService { get; set; }

        public List<ItemInstance> ItemInstances = new();

        protected override async Task OnParametersSetAsync()
        {
            await base.OnParametersSetAsync();
            var avAttr = ItemService.GetItemAttributes();

            if (ItemInstances.Count > 0)
                ItemInstances.Clear();

            var stash = SelectedUser.Stash;
            foreach (var invItem in stash)
            {
                ItemInstances.Add(new ItemInstance(ItemService.GetItem(invItem.ItemId), invItem, ItemService.GetItemEquipmentSlot(invItem.ItemId), avAttr));
            }
            foreach (var character in SelectedUser.Characters)
            {

                foreach (var invItem in character.InventoryItems)
                {
                    ItemInstances.Add(new ItemInstance(ItemService.GetItem(invItem.ItemId), invItem, ItemService.GetItemEquipmentSlot(invItem.ItemId), avAttr));
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

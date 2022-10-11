using Microsoft.AspNetCore.Components;
using RavenNest.Blazor.Services;
using RavenNest.BusinessLogic.Extended;
using RavenNest.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace RavenNest.Blazor.Components
{
    public partial class InventoryManagerComponent : ComponentBase
    {
        [Parameter] public List<ItemInstance> ItemInstances { get; set; }
        [Parameter] public WebsiteAdminUser User { get; set; }
        [Parameter] public EventCallback<ItemInstance> OnItemUpdate { get; set; }
        [Parameter] public RenderFragment ChildContent { get; set; }
        [Inject] ItemService ItemService { get; set; }

        public ItemInstance Payload { get; set; }

        public async Task UpdateItemLocationAsync(Location storageLocation, object newOwnerId)
        {
            var itemInstance = ItemInstances.SingleOrDefault(x => x.Id == Payload.Id);
            if (itemInstance != null)
            {
                //set location
                await OnItemUpdate.InvokeAsync(Payload);
            }
        }

        public bool CanDrop(Location storageLocation, WebsiteAdminPlayer charactersBag)
        {
            switch (storageLocation)
            {
                case Location.Equipment:
                    if (charactersBag == null)
                        return false;
                    var item = ItemService.GetItem(Payload.Id);
                    if (item == null)
                        return false;

                    return RavenNest.BusinessLogic.Providers.PlayerInventory.CanEquipItem(item, charactersBag);
                case Location.Bank:
                case Location.CharactersBag:
                    return true;
            }
            return false;
        }
    }
}

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
        [Parameter] public bool CanManage { get; set; }
        [Inject] ItemService ItemService { get; set; }
        [Inject] InventoryService InventoryService { get; set; }

        public ItemInstance Payload { get; set; }

        public async Task UpdateItemLocationAsync(Location storageLocation, Guid newOwnerId)
        {
            var itemInstance = ItemInstances.SingleOrDefault(x => x.Id == Payload.Id);

            if (itemInstance != null)
            {
                //set location
                Payload = SendToNewLocation(storageLocation, newOwnerId, ref itemInstance);
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
                    var item = ItemService.GetItem(Payload.ItemId);
                    if (item == null)
                        return false;

                    return CanEquipItem(item, charactersBag);
                case Location.Bank:
                case Location.CharactersBag:
                    return true;
            }
            return false;
        }
        public bool CanEquipItem(Item item, WebsiteAdminPlayer charactersBag)
        {
            return RavenNest.BusinessLogic.Providers.PlayerInventory.CanEquipItem(item, charactersBag);
        }

        public ItemInstance SendToNewLocation(Location storageLocation, Guid newOwnerId, ref ItemInstance itemInstance)
        {
            if (itemInstance.Location.Equals(Location.Equipment))
            {
                if (!InventoryService.UnequipItem(ref itemInstance))
                    return null;
            }

            switch (storageLocation)
            {
                case Location.Equipment:

                    if (InventoryService.IsNewOwner(newOwnerId, itemInstance))
                        InventoryService.SendToCharacter(newOwnerId, ref itemInstance);

                    var equippedInSlot = InventoryService.GetItemInEquipmentSlot(newOwnerId, itemInstance.EquipmentSlot);

                    if (equippedInSlot != null)
                    {
                        var curEquippedItem = ItemInstances.SingleOrDefault(x => x.Id == equippedInSlot);
                        InventoryService.UnequipItem(ref curEquippedItem); //unequip ahead of time to keep ItemInstance records valid
                    }

                    InventoryService.EquipItem(ref itemInstance);
                    break;
                case Location.Bank:
                    InventoryService.SendToStash(newOwnerId, ref itemInstance);
                    break;
                case Location.CharactersBag:
                    InventoryService.SendToCharacter(newOwnerId, ref itemInstance);
                    break;
            }

            return null;
        }
    }
}


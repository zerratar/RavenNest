using Microsoft.AspNetCore.Components;
using RavenNest.Blazor.Services;
using RavenNest.BusinessLogic.Extended;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RavenNest.Blazor.Components
{
    public partial class InventoryStorageComponent : ComponentBase
    {
        [CascadingParameter] InventoryManagerComponent InventoryManagerComp { get; set; }
        [Parameter] public Location StorageLocation { get; set; }
        [Parameter] public Guid OwnerId { get; set; }
        List<ItemInstance> ItemInstances = new();

        string dropClass = "";
        WebsiteAdminPlayer CharactersBag;
        protected override void OnParametersSet()
        {
            ItemInstances.Clear();
            ItemInstances.AddRange(InventoryManagerComp.ItemInstances.Where(x => x.Location == StorageLocation && x.OwnerId == OwnerId));

            if(!StorageLocation.Equals(Location.Bank))
                CharactersBag = InventoryManagerComp.User.Characters.SingleOrDefault(x => x.Id == OwnerId);

        }

        private void HandleDragEnter()
        {
            if (checkSelf(InventoryManagerComp.Payload))
                return;
            dropClass = CanDrop(InventoryManagerComp.Payload) ? "can-drop" : "no-drop";
        }

        private bool checkSelf(ItemInstance Payload)
        {
            return Payload.Location == StorageLocation && Payload.OwnerId == OwnerId;
        }
        private bool CanDrop(ItemInstance Payload)
        {
            return InventoryManagerComp.CanDrop(StorageLocation, CharactersBag);
        }

        private void HandleDragLeave()
        {
            dropClass = "";
        }

        private async Task HandleDrop()
        {
            dropClass = "";

            if (checkSelf(InventoryManagerComp.Payload))
                return;

            await InventoryManagerComp.UpdateItemLocationAsync(StorageLocation, OwnerId);
        }

        private string GetIdentifier(WebsiteAdminPlayer character)
        {
            if (character == null) return "";

            if (character.Identifier == null) return character.CharacterIndex.ToString();
            return character.Alias;
        }
        private ItemInstance GetItemInstance(BusinessLogic.Providers.EquipmentSlot slot)
        {
            return ItemInstances.SingleOrDefault(x => x.EquipmentSlot.Equals(slot));
        }
    }
}

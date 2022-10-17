using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Hosting;
using RavenNest.Blazor.Services;
using RavenNest.BusinessLogic.Extended;
using RavenNest.BusinessLogic.Providers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace RavenNest.Blazor.Components
{
    public partial class InventoryStorageComponent : ComponentBase
    {
        [CascadingParameter] InventoryManagerComponent InventoryManagerComp { get; set; }
        [Parameter] public Location StorageLocation { get; set; }
        [Parameter] public Guid OwnerId { get; set; }
        [Inject]
        IWebHostEnvironment WebHostEnv { get; set; }
        List<ItemInstance> ItemInstances = new();

        string dropClass = "no-drop";
        WebsiteAdminPlayer CharactersBag;

        protected override void OnParametersSet()
        {
            ItemInstances.Clear();
            ItemInstances.AddRange(InventoryManagerComp.ItemInstances.Where(x => x.Location == StorageLocation && x.OwnerId == OwnerId));

            if(!StorageLocation.Equals(Location.Bank))
                CharactersBag = InventoryManagerComp.User.Characters.SingleOrDefault(x => x.Id == OwnerId);

        }
        public string SetEquipmentSlotSrc(EquipmentSlot? slot)
        {
            if (!slot.HasValue)
                return null;

            string path = "/imgs/icons/inventory_slot/";
            string outputSrc = path + slot.ToString().ToLower() + ".png";
            var wwwroot = WebHostEnv.WebRootPath;
            return File.Exists(wwwroot + outputSrc) ? outputSrc : null;
        }

        private void HandleDragEnter()
        {
            if (CheckSelf(InventoryManagerComp.Payload))
                return;
            dropClass = CanDrop(InventoryManagerComp.Payload) ? "can-drop" : "cannot-drop";
        }

        private bool CheckSelf(ItemInstance Payload)
        {
            if (Payload == null)
                return true;
            return Payload.Location == StorageLocation && Payload.OwnerId == OwnerId;
        }
        private bool CanDrop(ItemInstance Payload)
        {
            if (Payload == null)
                return false;

            return InventoryManagerComp.CanDrop(StorageLocation, CharactersBag);
        }

        private void HandleDragLeave()
        {
            dropClass = "no-drop";
        }

        private async Task HandleDrop()
        {
            dropClass = "no-drop";

            if (CheckSelf(InventoryManagerComp.Payload))
                return;

            await InventoryManagerComp.UpdateItemLocationAsync(StorageLocation, OwnerId);
        }

        private string GetIdentifier(WebsiteAdminPlayer character)
        {
            if (character == null) return "";

            if (character.Identifier == null) return character.CharacterIndex.ToString();
            return character.Alias;
        }
        private ItemInstance GetItemInstance(EquipmentSlot slot)
        {
            return ItemInstances.SingleOrDefault(x => x.EquipmentSlot.Equals(slot));
        }
    }
}

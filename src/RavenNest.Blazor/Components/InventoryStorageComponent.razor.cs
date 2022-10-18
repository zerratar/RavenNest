using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Hosting;
using Microsoft.JSInterop;
using Newtonsoft.Json;
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
        [Inject]
        IJSRuntime JS { get; set; }
        List<ItemInstance> ItemInstances = new();

        string dropClass = "no-drop";
        WebsiteAdminPlayer CharactersBag;
        private int dragEnterCount = 0;
        private int dragLeaveCount = 0;
        private int DragCount { get { return dragEnterCount - dragLeaveCount; } }

        protected override void OnParametersSet()
        {
            ItemInstances.Clear();
            ItemInstances.AddRange(InventoryManagerComp.ItemInstances.Where(x => x.Location == StorageLocation && x.OwnerId == OwnerId));

            if (!StorageLocation.Equals(Location.Bank))
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

        //Drag Events Handler
        private void HandleDragEnter(DragEventArgs args)
        {
            if (DragCount == 0)
            {
                if (!CheckSelf(InventoryManagerComp.Payload))
                    dropClass = CanDrop(InventoryManagerComp.Payload) ? "can-drop" : "cannot-drop";
            }
            dragEnterCount++;
        }

        private void HandleDragOver(DragEventArgs args)
        {
            args.DataTransfer.DropEffect = "move";
        }

        private void HandleDragLeave(DragEventArgs args)
        {
            dragLeaveCount++;
            if (DragCount == 0)
                dropClass = "no-drop";
        }
        private void HandleDragEnd(DragEventArgs args)
        {
            dragLeaveCount = 0;
            dragEnterCount = 0;
        }


        private async Task HandleDrop(DragEventArgs args)
        {
            dropClass = "no-drop";

            if (CheckSelf(InventoryManagerComp.Payload))
                return;

            await InventoryManagerComp.UpdateItemLocationAsync(StorageLocation, OwnerId);
        }

        //Helper Functions

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

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Hosting;
using RavenNest.BusinessLogic.Providers;
using RavenNest.BusinessLogic.Extended;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using EquipmentSlot = RavenNest.BusinessLogic.Providers.EquipmentSlot;

namespace RavenNest.Blazor.Components
{
    public partial class InventoryItemComponent : ComponentBase
    {
        [CascadingParameter] InventoryManagerComponent InventoryManagerComponent { get; set; }
        [Parameter] public ItemInstance Item { get; set; }
        [Parameter] public EquipmentSlot? Slot { get; set; }
        [Inject]
        IWebHostEnvironment WebHostEnv { get; set; }

        private string ItemImageSrc;
        private string SlotImageSrc;
        private string ItemAmountFormatted;

        protected override async Task OnParametersSetAsync()
        {
            await base.OnParametersSetAsync();

            if (Slot != null)
            {
                SlotImageSrc = SetEquipmentSlotSrc(Slot) ?? "/imgs/icons/inventory_slot/none.png";
            }

            if (Item != null)
            {
                ItemImageSrc = SetItemImageSrc(Item.ItemId, Item.Tag);
                ItemAmountFormatted = GetItemAmount(Item.Amount ?? 0);
            }
        }

        public string GetItemAmount(long item)
        {
            var value = item;
            if (value >= 1000_000)
            {
                var mils = value / 1000000.0;
                return Math.Round(mils) + "M";
            }
            else if (value > 1000)
            {
                var ks = value / 1000m;
                return Math.Round(ks) + "K";
            }

            return item.ToString();
        }
        public string SetItemImageSrc(Guid itemId, string tag)
        {
            if (tag != null)
            {
                return $"/api/twitch/logo/{tag}";
            }
            return $"/imgs/items/{itemId}.png";
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

        private void HandleDragStart(ItemInstance selectedItem)
        {
            InventoryManagerComponent.Payload = selectedItem;
        }
    }
}

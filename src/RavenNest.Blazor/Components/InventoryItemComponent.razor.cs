using Microsoft.AspNetCore.Components;
using RavenNest.BusinessLogic.Extended;
using System;
using System.Threading.Tasks;

namespace RavenNest.Blazor.Components
{
    public partial class InventoryItemComponent : ComponentBase
    {
        [CascadingParameter] InventoryManagerComponent InventoryManagerComponent { get; set; }
        [Parameter] public ItemInstance Item { get; set; }


        private string ItemImageSrc;
        private string ItemAmountFormatted;

        protected override async Task OnParametersSetAsync()
        {
            await base.OnParametersSetAsync();
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

        private void HandleDragStart(ItemInstance selectedItem)
        {
            InventoryManagerComponent.Payload = selectedItem;
        }
    }
}

using Microsoft.AspNetCore.Components;
using RavenNest.BusinessLogic.Extended;
using System;

namespace RavenNest.Blazor.Components
{
    public partial class ItemTooltip : ComponentBase
    {
        [CascadingParameter] InventoryManagerComponent InventoryManagerComponent { get; set; }
        [Parameter] public RenderFragment ChildContent { get; set; }
        [Parameter] public ItemInstance Item { get; set; }
        [Parameter] public bool? Extended { get; set; }
        public bool DisplayDetail { get; set; }

        protected override void OnParametersSet()
        {
            base.OnParametersSet();
            DisplayDetail = Extended ?? false;
        }

            private void CloseItemDetails()
        {
            
        }

        private void DoItemAction(Location newLocation, Guid newOwnerId)
        {
            CloseItemDetails();
        }
    }




}

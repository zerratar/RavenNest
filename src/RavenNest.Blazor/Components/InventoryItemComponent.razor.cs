using Microsoft.AspNetCore.Components;
using RavenNest.BusinessLogic.Extended;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RavenNest.Blazor.Components
{
    public partial class InventoryItemComponent : ComponentBase
    {
        [CascadingParameter] InventoryManagerComponent InventoryManagerComponent { get; set; }
        [Parameter] public ItemInstance Item { get; set; }

        private enum ItemView
        {
            None,
            Detailed,
            Summary
        }

        private void HandleDragStart(ItemInstance selectedItem)
        {
            InventoryManagerComponent.Payload = selectedItem;
        }

        private void HandleClickEvent()
        {}

    }
}

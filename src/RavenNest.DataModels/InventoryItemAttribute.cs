using System;

namespace RavenNest.DataModels
{
    public partial class InventoryItemAttribute : Entity<InventoryItemAttribute>
    {
        private Guid id; public Guid Id { get => id; set => Set(ref id, value); }
        private Guid inventoryItemId; public Guid InventoryItemId { get => inventoryItemId; set => Set(ref inventoryItemId, value); }
        private Guid attributeId; public Guid AttributeId { get => attributeId; set => Set(ref attributeId, value); }
        private string value; public string Value { get => value; set => Set(ref this.value, value); }

    }
}

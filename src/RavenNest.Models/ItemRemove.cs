using System;

namespace RavenNest.Models
{
    public class ItemRemove
    {
        public Guid PlayerId { get; set; }
        public Guid InventoryItemId { get; set; }
        public Guid ItemId { get; set; }
        public long Amount { get; set; }
    }
}

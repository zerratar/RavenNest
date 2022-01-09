using System;

namespace RavenNest.Models
{
    public class ItemRemove
    {
        public string UserId { get; set; }
        public Guid InventoryItemId { get; set; }
        public Guid ItemId { get; set; }
        public long Amount { get; set; }
    }
}

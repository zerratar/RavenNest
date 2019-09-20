using System;

namespace RavenNest.Models
{
    public class InventoryItem
    {
        public Guid Id { get; set; }

        public Guid ItemId { get; set; }

        public long Amount { get; set; }

        public bool Equipped { get; set; }
    }
}
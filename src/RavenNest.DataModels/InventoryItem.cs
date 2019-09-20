using System;

namespace RavenNest.DataModels
{
    public partial class InventoryItem
    {
        public Guid Id { get; set; }
        public Guid CharacterId { get; set; }
        public Guid ItemId { get; set; }
        public long? Amount { get; set; }
        public bool Equipped { get; set; }

        public Character Character { get; set; }
        public Item Item { get; set; }
    }
}

using System;

namespace RavenNest.Models
{
    public class ItemCraftingRequirement
    {
        public Guid Id { get; set; }
        public Guid ItemId { get; set; }
        public Guid ResourceItemId { get; set; }
        public int Amount { get; set; }
    }
}
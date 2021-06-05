using System;

namespace RavenNest.Models
{
    public class ScrollInfo
    {
        public Guid ItemId { get; set; }
        public decimal Amount { get; set; }
        public string Name { get; set; }
        public ScrollInfo() { }
        public ScrollInfo(Guid itemId, string name, decimal amount)
        {
            ItemId = itemId;
            Name = name;
            Amount = amount;
        }
    }
}

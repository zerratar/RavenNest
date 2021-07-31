using System;

namespace RavenNest.Models
{
    public class ScrollInfo
    {
        public Guid ItemId { get; set; }
        public double Amount { get; set; }
        public string Name { get; set; }
        public ScrollInfo() { }
        public ScrollInfo(Guid itemId, string name, double amount)
        {
            ItemId = itemId;
            Name = name;
            Amount = amount;
        }
    }
}

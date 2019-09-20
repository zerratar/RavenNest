using System;

namespace RavenNest.Models
{
    public class ItemTradeUpdate
    {
        public string SellerId { get; set; }
        public string BuyerId { get; set; }
        public Guid ItemId { get; set; }
        public long Amount { get; set; }
        public decimal Cost { get; set; }
    }
}
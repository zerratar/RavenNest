using System;

namespace RavenNest.Models
{
    public class MarketItem
    {
        public Guid Id { get; set; }
        public string SellerUserId { get; set; }
        public Guid ItemId { get; set; }
        public long Amount { get; set; }
        public decimal PricePerItem { get; set; }
    }
}
using System;

namespace RavenNest.Models
{
    public class ItemTradeUpdate
    {
        public Guid SellerPlayerId { get; set; }
        public Guid BuyerPlayerId { get; set; }
        public Guid ItemId { get; set; }
        public long Amount { get; set; }
        public double Cost { get; set; }
    }
}

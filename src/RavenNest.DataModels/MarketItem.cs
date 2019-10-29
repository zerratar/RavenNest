using System;

namespace RavenNest.DataModels
{
    public class MarketItem
    {
        public Guid Id { get; set; }
        public Guid SellerCharacterId { get; set; }
        public Guid ItemId { get; set; }
        public long Amount { get; set; }
        public decimal PricePerItem { get; set; }
        public DateTime Created { get; set; }
        //public Character SellerCharacter { get; set; }
        //public Item Item { get; set; }
    }
}
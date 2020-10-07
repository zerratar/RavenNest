using System;

namespace RavenNest.DataModels
{
    public class MarketItem : Entity<MarketItem>
    {
        private Guid id; public Guid Id { get => id; set => Set(ref id, value); }
        private Guid sellerCharacterId; public Guid SellerCharacterId { get => sellerCharacterId; set => Set(ref sellerCharacterId, value); }
        private Guid itemId; public Guid ItemId { get => itemId; set => Set(ref itemId, value); }
        private long amount; public long Amount { get => amount; set => Set(ref amount, value); }
        private decimal pricePerItem; public decimal PricePerItem { get => pricePerItem; set => Set(ref pricePerItem, value); }
        private string tag; public string Tag { get => tag; set => Set(ref tag, value); }
        private DateTime created; public DateTime Created { get => created; set => Set(ref created, value); }
    }
}

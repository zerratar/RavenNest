using System;

namespace RavenNest.DataModels
{
    public class VendorTransaction : Entity<VendorTransaction>
    {
        private Guid id; public Guid Id { get => id; set => Set(ref id, value); }
        private Guid itemId; public Guid ItemId { get => itemId; set => Set(ref itemId, value); }
        private long amount; public long Amount { get => amount; set => Set(ref amount, value); }
        private double pricePerItem; public double PricePerItem { get => pricePerItem; set => Set(ref pricePerItem, value); }
        private double totalPrice; public double TotalPrice { get => totalPrice; set => Set(ref totalPrice, value); }
        private Guid sellerCharacterId; public Guid SellerCharacterId { get => sellerCharacterId; set => Set(ref sellerCharacterId, value); }
        private DateTime created; public DateTime Created { get => created; set => Set(ref created, value); }
    }
}

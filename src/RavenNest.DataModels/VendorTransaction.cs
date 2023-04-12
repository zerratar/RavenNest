using System;

namespace RavenNest.DataModels
{
    public class VendorTransaction : Entity<VendorTransaction>
    {
        private Guid itemId; public Guid ItemId { get => itemId; set => Set(ref itemId, value); }
        private long amount; public long Amount { get => amount; set => Set(ref amount, value); }
        private long pricePerItem; public long PricePerItem { get => pricePerItem; set => Set(ref pricePerItem, value); }
        private long totalPrice; public long TotalPrice { get => totalPrice; set => Set(ref totalPrice, value); }
        private Guid characterId; public Guid CharacterId { get => characterId; set => Set(ref characterId, value); }
        private bool transactionType; public bool TransactionType { get => transactionType; set => Set(ref transactionType, value); }
        private DateTime created; public DateTime Created { get => created; set => Set(ref created, value); }
    }
}

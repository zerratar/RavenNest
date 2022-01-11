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

    public class GiftTransaction : Entity<GiftTransaction>
    {
        private Guid id; public Guid Id { get => id; set => Set(ref id, value); }
        private Guid fromCharacterId; public Guid FromCharacterId { get => fromCharacterId; set => Set(ref fromCharacterId, value); }
        private Guid toCharacterId; public Guid ToCharacterId { get => toCharacterId; set => Set(ref toCharacterId, value); }
        private Guid itemId; public Guid ItemId { get => itemId; set => Set(ref itemId, value); }
        private long amount; public long Amount { get => amount; set => Set(ref amount, value); }
        private string tag; public string Tag { get => tag; set => Set(ref tag, value); }
        private string name; public string Name { get => name; set => Set(ref name, value); }
        private string enchantment; public string Enchantment { get => enchantment; set => Set(ref enchantment, value); }
        private Guid? transmogrificationId; public Guid? TransmogrificationId { get => transmogrificationId; set => Set(ref transmogrificationId, value); }
        private int? flags; public int? Flags { get => flags; set => Set(ref flags, value); }
        private DateTime created; public DateTime Created { get => created; set => Set(ref created, value); }
    }

}

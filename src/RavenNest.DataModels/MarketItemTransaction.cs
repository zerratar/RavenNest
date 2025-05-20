using RavenNest.DataAnnotations;
using System;

namespace RavenNest.DataModels
{
    public partial class MarketItemTransaction : Entity<MarketItemTransaction>
    {
        [PersistentData] private Guid itemId;
        [PersistentData] private long amount;
        [PersistentData] private double pricePerItem;
        [PersistentData] private double totalPrice;
        [PersistentData] private Guid buyerCharacterId;
        [PersistentData] private Guid sellerCharacterId;
        [PersistentData] private DateTime created;
    }
}

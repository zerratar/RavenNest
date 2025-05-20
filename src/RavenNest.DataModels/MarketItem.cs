using RavenNest.DataAnnotations;
using System;

namespace RavenNest.DataModels
{
    public partial class MarketItem : Entity<MarketItem>
    {
        [PersistentData] private Guid sellerCharacterId;
        [PersistentData] private Guid itemId;
        [PersistentData] private long amount;
        [PersistentData] private double pricePerItem;
        [PersistentData] private string tag;
        [PersistentData] private DateTime? expires;
        [PersistentData] private DateTime created;
        [PersistentData] private string name;
        [PersistentData] private string enchantment;
        [PersistentData] private Guid? transmogrificationId;
        [PersistentData] private int? flags;
    }
}

using RavenNest.DataAnnotations;
using System;

namespace RavenNest.DataModels
{
    public partial class VendorTransaction : Entity<VendorTransaction>
    {
        [PersistentData] private Guid itemId;
        [PersistentData] private long amount;
        [PersistentData] private long pricePerItem;
        [PersistentData] private long totalPrice;
        [PersistentData] private Guid characterId;
        [PersistentData] private bool transactionType;
        [PersistentData] private DateTime created;
    }
}

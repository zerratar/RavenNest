using RavenNest.DataAnnotations;
using System;

namespace RavenNest.DataModels
{
    public partial class VendorItem : Entity<VendorItem>
    {
        [PersistentData] private long stock;
        [PersistentData] private Guid itemId;
        [PersistentData] private string enchantment;
        [PersistentData] private Guid? transmogrificationId;
        [PersistentData] private int? flags;
    }
}

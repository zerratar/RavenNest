using System;

namespace RavenNest.DataModels
{
    public partial class VendorItem : Entity<VendorItem>
    {
        private long stock; public long Stock { get => stock; set => Set(ref stock, value); }
        private Guid itemId; public Guid ItemId { get => itemId; set => Set(ref itemId, value); }
        private string enchantment; public string Enchantment { get => enchantment; set => Set(ref enchantment, value); }
        private Guid? transmogrificationId; public Guid? TransmogrificationId { get => transmogrificationId; set => Set(ref transmogrificationId, value); }
        private int? flags; public int? Flags { get => flags; set => Set(ref flags, value); }
    }
}

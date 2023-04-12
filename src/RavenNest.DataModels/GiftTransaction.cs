using System;

namespace RavenNest.DataModels
{
    public class GiftTransaction : Entity<GiftTransaction>
    {
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

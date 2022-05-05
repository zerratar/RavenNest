using System;

namespace RavenNest.DataModels
{
    public partial class ClanBankItem : Entity<ClanBankItem>
    {
        private Guid id; public Guid Id { get => id; set => Set(ref id, value); }
        private Guid userId; public Guid UserId { get => userId; set => Set(ref userId, value); }
        private Guid itemId; public Guid ItemId { get => itemId; set => Set(ref itemId, value); }
        private long amount; public long Amount { get => amount; set => Set(ref amount, value); }
        private string name; public string Name { get => name; set => Set(ref name, value); }
        private string enchantment; public string Enchantment { get => enchantment; set => Set(ref enchantment, value); }
        private string tag; public string Tag { get => tag; set => Set(ref tag, value); }
        private bool soulbound; public bool Soulbound { get => soulbound; set => Set(ref soulbound, value); }
        private Guid? transmogrificationId; public Guid? TransmogrificationId { get => transmogrificationId; set => Set(ref transmogrificationId, value); }
        private int flags; public int Flags { get => flags; set => Set(ref flags, value); }
    }
}

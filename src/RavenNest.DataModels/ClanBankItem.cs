using RavenNest.DataAnnotations;
using System;

namespace RavenNest.DataModels
{
    public partial class ClanBankItem : Entity<ClanBankItem>
    {
        [PersistentData] private Guid userId;
        [PersistentData] private Guid itemId;
        [PersistentData] private long amount;
        [PersistentData] private string name;
        [PersistentData] private string enchantment;
        [PersistentData] private string tag;
        [PersistentData] private bool soulbound;
        [PersistentData] private Guid? transmogrificationId;
        [PersistentData] private int flags;
    }
}

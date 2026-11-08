using RavenNest.DataAnnotations;
using System;

namespace RavenNest.DataModels
{
    public partial class UserBankItem : Entity<UserBankItem>
    {
        [PersistentData] private Guid userId;
        [PersistentData] private Guid itemId;
        [PersistentData] private long amount;
        [PersistentData] private string name;
        [PersistentData] private string enchantment;
        /// <summary>Gems in the item's sockets (RavenNest.Combat.Gems): a gem item id per socket, comma separated; null without gems.</summary>
        [PersistentData] private string sockets;
        [PersistentData] private string tag;
        [PersistentData] private bool soulbound;
        [PersistentData] private Guid? transmogrificationId;
        [PersistentData] private int flags;
    }
}

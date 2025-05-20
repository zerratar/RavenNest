using RavenNest.DataAnnotations;
using System;

namespace RavenNest.DataModels
{
    public partial class GiftTransaction : Entity<GiftTransaction>
    {
        [PersistentData] private Guid fromCharacterId;
        [PersistentData] private Guid toCharacterId;
        [PersistentData] private Guid itemId;
        [PersistentData] private long amount;
        [PersistentData] private string tag;
        [PersistentData] private string name;
        [PersistentData] private string enchantment;
        [PersistentData] private Guid? transmogrificationId;
        [PersistentData] private int? flags;
        [PersistentData] private DateTime created;
    }

}

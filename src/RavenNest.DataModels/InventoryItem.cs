using RavenNest.DataAnnotations;
using System;

namespace RavenNest.DataModels
{
    // Want to rename this to ItemInstance
    public partial class InventoryItem : Entity<InventoryItem>
    {
        [PersistentData] private Guid characterId;
        [PersistentData] private Guid itemId;
        [PersistentData] private string name;
        [PersistentData] private string enchantment;
        [PersistentData] private long? amount;
        [PersistentData] private bool equipped;
        [PersistentData] private string tag;
        [PersistentData] private bool soulbound;

        [PersistentData] private Guid? transmogrificationId;
        [PersistentData] private int? flags;

        // private Guid? creatorId; public Guid? CreatorId { get => creatorId; set => Set(ref creatorId, value); }

        public override string ToString()
        {
            return $"{Name} (x{Amount}) ID: {Id}";
        }

    }
}

using System;

namespace RavenNest.DataModels
{
    public class NPCItemDrop : Entity<NPCItemDrop>
    {
        private Guid npcId; public Guid NpcId { get => npcId; set => Set(ref npcId, value); }
        private Guid itemId; public Guid ItemId { get => itemId; set => Set(ref itemId, value); }
        private double dropChance; public double DropChance { get => dropChance; set => Set(ref dropChance, value); }
    }
}

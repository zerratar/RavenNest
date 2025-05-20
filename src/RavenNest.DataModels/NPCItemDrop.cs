using RavenNest.DataAnnotations;
using System;

namespace RavenNest.DataModels
{
    public partial class NPCItemDrop : Entity<NPCItemDrop>
    {
        [PersistentData] private Guid npcId;
        [PersistentData] private Guid itemId;
        [PersistentData] private double dropChance;
    }
}

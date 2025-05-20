using RavenNest.DataAnnotations;
using System;

namespace RavenNest.DataModels
{
    public partial class NPCSpawn : Entity<NPCSpawn>
    {
        [PersistentData] private Guid npcId;
        [PersistentData] private int x;
        [PersistentData] private int y;
        [PersistentData] private int z;
        [PersistentData] private double respawnInterval;
    }
}

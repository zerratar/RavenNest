using System;

namespace RavenNest.DataModels
{
    public class NPCSpawn : Entity<NPCSpawn>
    {
        private Guid id; public Guid Id { get => id; set => Set(ref id, value); }

        private Guid npcId; public Guid NpcId { get => npcId; set => Set(ref npcId, value); }
        private int x; public int X { get => x; set => Set(ref x, value); }
        private int y; public int Y { get => y; set => Set(ref y, value); }
        private int z; public int Z { get => z; set => Set(ref z, value); }
        private double respawnInterval; public double RespawnInterval { get => respawnInterval; set => Set(ref respawnInterval, value); }
    }
}

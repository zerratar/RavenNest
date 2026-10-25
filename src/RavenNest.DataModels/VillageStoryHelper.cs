using RavenNest.DataAnnotations;
using System;

namespace RavenNest.DataModels
{
    /// <summary>What a character added to a step of a streamer's story line: items brought, enemies defeated, things found.</summary>
    public partial class VillageStoryHelper : Entity<VillageStoryHelper>
    {
        [PersistentData] private Guid villageStoryId;
        [PersistentData] private int step;
        [PersistentData] private Guid characterId;
        [PersistentData] private long amount;
        [PersistentData] private DateTime updated;
    }
}

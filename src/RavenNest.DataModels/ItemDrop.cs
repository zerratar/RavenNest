using RavenNest.DataAnnotations;
using System;

namespace RavenNest.DataModels
{
    public partial class ItemDrop : Entity<ItemDrop>
    {
        [PersistentData] private Guid itemId;
        // 0 for raid, 1 for dungeon, add more if necessary
        [PersistentData] private int tier;
        [PersistentData] private double minDropRate;
        [PersistentData] private double maxDropRate;
        [PersistentData] private int? dropStartMonth;
        [PersistentData] private int? dropDurationMonths;

        /// <summary>
        ///     Whether or not the drop rate should be lower if item is already owned by the player.
        /// </summary>        
        [PersistentData] private bool uniqueDrop;

        [PersistentData] private int slayerLevelRequirement;
    }
}

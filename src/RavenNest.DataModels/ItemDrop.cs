using System;

namespace RavenNest.DataModels
{
    public class ItemDrop : Entity<ItemDrop>
    {
        private Guid itemId; public Guid ItemId { get => itemId; set => Set(ref itemId, value); }
        // 0 for raid, 1 for dungeon, add more if necessary
        private int sourceType; public int SourceType { get => sourceType; set => Set(ref sourceType, value); }
        private int minDifficultyTier; public int MinDifficultyTier { get => minDifficultyTier; set => Set(ref minDifficultyTier, value); }
        private double minDropRate; public double MinDropRate { get => minDropRate; set => Set(ref minDropRate, value); }
        private double maxDropRate; public double MaxDropRate { get => maxDropRate; set => Set(ref maxDropRate, value); }
        private DateTime? dropStart; public DateTime? DropStart { get => dropStart; set => Set(ref dropStart, value); }
        private TimeSpan? dropDuration; public TimeSpan? DropDuration { get => dropDuration; set => Set(ref dropDuration, value); }

        private bool uniqueDrop;

        /// <summary>
        ///     Whether or not the drop rate should be lower if item is already owned by the player.
        /// </summary>
        public bool UniqueDrop { get => uniqueDrop; set => Set(ref uniqueDrop, value); }
        private int slayerLevelRequirement; public int SlayerLevelRequirement { get => slayerLevelRequirement; set => Set(ref slayerLevelRequirement, value); }
    }
}

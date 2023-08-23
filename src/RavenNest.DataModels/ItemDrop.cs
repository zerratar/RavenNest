using System;

namespace RavenNest.DataModels
{
    public class ItemDrop : Entity<ItemDrop>
    {
        private Guid itemId; public Guid ItemId { get => itemId; set => Set(ref itemId, value); }
        // 0 for raid, 1 for dungeon, add more if necessary
        private int tier; public int Tier { get => tier; set => Set(ref tier, value); }
        private double minDropRate; public double MinDropRate { get => minDropRate; set => Set(ref minDropRate, value); }
        private double maxDropRate; public double MaxDropRate { get => maxDropRate; set => Set(ref maxDropRate, value); }
        private int? dropStartMonth; public int? DropStartMonth { get => dropStartMonth; set => Set(ref dropStartMonth, value); }
        private int? dropDurationMonths; public int? DropDurationMonths { get => dropDurationMonths; set => Set(ref dropDurationMonths, value); }

        private bool uniqueDrop;

        /// <summary>
        ///     Whether or not the drop rate should be lower if item is already owned by the player.
        /// </summary>
        public bool UniqueDrop { get => uniqueDrop; set => Set(ref uniqueDrop, value); }
        private int slayerLevelRequirement; public int SlayerLevelRequirement { get => slayerLevelRequirement; set => Set(ref slayerLevelRequirement, value); }
    }
}

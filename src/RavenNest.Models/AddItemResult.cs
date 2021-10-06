using System;

namespace RavenNest.Models
{
    public enum AddItemResult
    {
        Failed,
        Added,
        AddedAndEquipped
    }

    public class CraftItemResult
    {
        public CraftItemResultStatus Status { get; set; }
        public Guid ItemId { get; set; }
        public int Value { get; set; }
        public static CraftItemResult NoSuchItem => new CraftItemResult { Status = CraftItemResultStatus.UnknownItem };
        public static CraftItemResult Error => new CraftItemResult { Status = CraftItemResultStatus.Error };
        public static CraftItemResult InsufficientResources => new CraftItemResult { Status = CraftItemResultStatus.InsufficientResources };

        public static CraftItemResult TooLowLevel(Guid id, int requiredCraftingLevel)
        {
            return new CraftItemResult
            {
                ItemId = id,
                Status = CraftItemResultStatus.LevelTooLow,
                Value = requiredCraftingLevel
            };
        }
    }

    public enum CraftItemResultStatus
    {
        Success,
        PartialSuccess,
        LevelTooLow,
        InsufficientResources,
        UncraftableItem,
        UnknownItem,
        Error
    }
}

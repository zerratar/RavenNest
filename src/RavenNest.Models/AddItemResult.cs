using System;

namespace RavenNest.Models
{
    public enum AddItemResult
    {
        Failed,
        Added,
        AddedAndEquipped,

        BadItem,
        BadCharacter,
        BadSession
    }

    public class AddItemInstanceResult
    {
        public AddItemResult Result { get; set; }
        public Guid InstanceId { get; set; }
        public string Message { get; set; }

        public static AddItemInstanceResult Failed { get; } = new AddItemInstanceResult
        {
            Result = AddItemResult.Failed,
            Message = "Unknown Error"
        };

        public static AddItemInstanceResult NoSuchItem(Guid itemId)
        {
            return new AddItemInstanceResult
            {
                Result = AddItemResult.BadItem,
                Message = $"No item with ID '{itemId}' exists."
            };
        }

        public static AddItemInstanceResult BadCharacter(string userId)
        {
            return new AddItemInstanceResult
            {
                Result = AddItemResult.BadCharacter,
                Message = $"No user with ID '{userId}' in this session"
            };
        }

        public static AddItemInstanceResult NoSuchSession()
        {
            return new AddItemInstanceResult
            {
                Result = AddItemResult.BadSession,
                Message = $"Session is no longer available."
            };
        }

        public static AddItemInstanceResult ItemAdded(Guid instanceId)
        {
            return new AddItemInstanceResult
            {
                Result = AddItemResult.Added,
                InstanceId = instanceId,
                Message = $"Item added!"
            };
        }
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

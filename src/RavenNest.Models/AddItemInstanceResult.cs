using System;

namespace RavenNest.Models
{
    public class AddItemInstanceResult
    {
        public AddItemResult Result { get; set; }
        public Guid InstanceId { get; set; }
        public string Message { get; set; }

        public static AddItemInstanceResult Failed()
        {
            return new AddItemInstanceResult
            {
                Result = AddItemResult.Failed,
                Message = "Unknown Error"
            };
        }

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
}

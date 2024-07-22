using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Game;
using RavenNest.DataModels;
using System;

namespace RavenNest.BusinessLogic.Extensions
{
    public static class InventoryItemExtensions
    {
        public static ReadOnlyInventoryItem AsReadOnly(this InventoryItem item, GameData gameData)
        {
            if (item == null) return default;
            return ReadOnlyInventoryItem.Create(gameData, item);
        }

        public static bool IsNull(this ReadOnlyInventoryItem item)
        {
            return item.Id == Guid.Empty;
        }

        public static bool IsNotNull(this ReadOnlyInventoryItem item)
        {
            return item.Id != Guid.Empty;
        }
    }
}

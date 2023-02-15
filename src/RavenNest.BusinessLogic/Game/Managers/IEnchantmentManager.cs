using RavenNest.BusinessLogic.Providers;
using RavenNest.DataModels;
using RavenNest.Models;
using System;

namespace RavenNest.BusinessLogic.Game
{
    public interface IEnchantmentManager
    {
        ItemEnchantmentResult EnchantItem(
            System.Guid sessionId,
            DataModels.ClanSkill clanSkill,
            DataModels.Character character,
            PlayerInventory inventory,
            ReadOnlyInventoryItem item,
            DataModels.Resources resources);
        ItemEnchantmentResult DisenchantItem(Guid sessionId, Character character, PlayerInventory inventory, ReadOnlyInventoryItem item);
    }
}

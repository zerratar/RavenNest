using RavenNest.BusinessLogic.Providers;
using RavenNest.DataModels;
using RavenNest.Models;

namespace RavenNest.BusinessLogic.Game
{
    public interface IEnchantmentManager
    {
        ItemEnchantmentResult EnchantItem(
            DataModels.ClanSkill clanSkill,
            DataModels.Character character,
            PlayerInventory inventory,
            ReadOnlyInventoryItem item,
            DataModels.Resources resources);
    }
}

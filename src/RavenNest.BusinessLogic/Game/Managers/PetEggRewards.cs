using System;
using System.Linq;
using RavenNest.BusinessLogic.Data;
using RavenNest.Combat;

namespace RavenNest.BusinessLogic.Game
{
    /// <summary>
    ///     Eggs from quests and events (the game's docs/battle-pets-design.md, "Eggs from quests and events"): a roll on a
    ///     source's table (RavenNest.Combat.PetEggs) and the egg into the character's inventory, where the inventory feed
    ///     tells the game. Dungeons, raids and tower chests roll their own in GameManager.
    /// </summary>
    public static class PetEggRewards
    {
        /// <summary>An egg with the chance given, or null; the egg is in the inventory when one comes back.</summary>
        public static PetSpecies Roll(GameData gameData, PlayerInventoryProvider inventoryProvider, Guid characterId, EggSource source, double chance, Random rng)
        {
            if (chance <= 0 || rng.NextDouble() >= chance) return null;
            var species = PetEggs.Pick(source, 0, rng.NextDouble());
            return species != null && Give(gameData, inventoryProvider, characterId, species) != Guid.Empty ? species : null;
        }

        /// <summary>The species' egg into the inventory: the stack's id, or empty when the egg item is missing.</summary>
        public static Guid Give(GameData gameData, PlayerInventoryProvider inventoryProvider, Guid characterId, PetSpecies species)
        {
            if (species == null) return Guid.Empty;
            var itemId = PetEggs.ItemId(species.Id);
            if (gameData.GetItem(itemId) == null) return Guid.Empty;
            var stack = inventoryProvider.Get(characterId)?.AddItem(itemId, 1).FirstOrDefault();
            return stack?.Id ?? Guid.Empty;
        }
    }
}

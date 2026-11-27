using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Data;
using RavenNest.Combat;
using RavenNest.DataModels;
using RavenNest.Models;
using GameSession = RavenNest.DataModels.GameSession;
using Character = RavenNest.DataModels.Character;
using ItemCategory = RavenNest.DataModels.ItemCategory;

namespace RavenNest.BusinessLogic.Game
{
    /// <summary>
    ///     Substitution's rare finds on the server (the game's docs/substitution-escape-idea.md): when a source's boss falls
    ///     (a tower part's chest from floor 2 up, a floor boss, a Legendary dungeon's boss, a raid boss) everyone who fought
    ///     rolls for a piece of gear with the magical attribute Substitution already on it (the smoke step), and, rarer, for
    ///     a Shadowstone (the shadow phase), a gem that is only ever found. The chances are RavenNest.Combat.Substitution's;
    ///     the finds come back as rewards for the game to show and announce. The game rolls the escapes in its fights.
    /// </summary>
    public class SubstitutionManager
    {
        private readonly GameData gameData;
        private readonly PlayerInventoryProvider inventories;
        private readonly ILogger<SubstitutionManager> logger;
        private readonly Random rng = new Random();

        public SubstitutionManager(GameData gameData, PlayerInventoryProvider inventories, ILogger<SubstitutionManager> logger)
        {
            this.gameData = gameData;
            this.inventories = inventories;
            this.logger = logger;
        }

        /// <summary>
        ///     A source's boss fell with these characters in the fight: each rolls for the attribute and for the gem.
        ///     <paramref name="pool"/> is the gear the find is made from (the source's own drop table; any piece in it the
        ///     character can wear, the best of them most likely).
        /// </summary>
        public EventItemReward[] BossFell(GameSession session, Guid[] characterIds, SubstitutionSource source, IReadOnlyList<ItemDrop> pool)
        {
            var rewards = new List<EventItemReward>();
            if (session == null || characterIds == null || characterIds.Length == 0) return rewards.ToArray();
            var sessionCharacters = gameData.GetActiveSessionCharacters(session);
            foreach (var id in characterIds.Distinct())
            {
                var character = sessionCharacters.FirstOrDefault(x => x.Id == id);
                if (character == null) continue;
                try
                {
                    var forced = Forced(character.Id);
                    if (forced == "gem" || rng.NextDouble() < Substitution.GemChance(source))
                    {
                        var gem = GiveGem(character, source);
                        if (gem != null) rewards.Add(gem);
                    }
                    if (forced == "attribute" || rng.NextDouble() < Substitution.AttributeChance(source))
                    {
                        var piece = GiveAttribute(character, source, pool);
                        if (piece != null) rewards.Add(piece);
                    }
                }
                catch (Exception exc)
                {
                    logger.LogError("Substitution: a find for " + character.Name + " failed: " + exc);
                }
            }
            if (rewards.Count > 0) gameData.Touch();
            return rewards.ToArray();
        }

        /// <summary>A Shadowstone, socket-ready, of a rolled tier (a floor boss's is the better of two rolls).</summary>
        private EventItemReward GiveGem(Character character, SubstitutionSource source)
        {
            var tier = Substitution.RollGemTier(rng.NextDouble(), rng.NextDouble(), source == SubstitutionSource.TowerFloorBoss);
            var id = Gems.ItemId(GemKind.Shadowstone, tier);
            if (gameData.GetItem(id) == null)
            {
                logger.LogError("Substitution: the {Gem} item is missing; nothing found.", Gems.Name(GemKind.Shadowstone, tier));
                return null;
            }
            var stack = inventories.Get(character.Id).AddItem(id, 1).FirstOrDefault();
            if (stack == null) return null;
            logger.LogWarning("Substitution: {Character} found a {Gem} from a {Source}.", character.Name, Gems.Name(GemKind.Shadowstone, tier), source);
            return new EventItemReward { Amount = 1, CharacterId = character.Id, ItemId = id, InventoryItemId = stack.Id };
        }

        /// <summary>
        ///     A piece of gear with Substitution on it (35 to 50% by the piece's level), named "... of Substitution". Picked
        ///     from the source's gear the character can wear, among the five best of it; none when the pool has no gear.
        ///     Not soulbound: a rare drop like any other, until it is enchanted.
        /// </summary>
        private EventItemReward GiveAttribute(Character character, SubstitutionSource source, IReadOnlyList<ItemDrop> pool)
        {
            var skills = gameData.GetCharacterSkills(character.SkillsId);
            var gear = (pool ?? Array.Empty<ItemDrop>())
                .Select(x => x != null ? gameData.GetItem(x.ItemId) : null)
                .Where(IsGear)
                .Distinct()
                .ToList();
            var wearable = gear.Where(x => skills != null && PlayerInventory.CanEquipItem(x, skills)).ToList();
            var from = (wearable.Count > 0 ? wearable : gear).OrderByDescending(GameMath.GetItemLevel).Take(5).ToList();
            if (from.Count == 0)
            {
                logger.LogWarning("Substitution: {Character} had a find from a {Source}, but its pool has no gear.", character.Name, source);
                return null;
            }

            var item = from[Math.Min(from.Count - 1, (int)(rng.NextDouble() * from.Count))];
            var chance = Substitution.AttributeChance(GameMath.GetItemLevel(item));
            var enchantment = Substitution.Enchantment(chance);
            var stack = inventories.Get(character.Id).AddItem(item.Id, 1, enchantment: enchantment, name: item.Name + " of Substitution").FirstOrDefault();
            if (stack == null) return null;
            logger.LogWarning("Substitution: {Character} found a {Item} with Substitution {Chance}% from a {Source}.", character.Name, item.Name, Math.Round(chance * 100), source);
            return new EventItemReward { Amount = 1, CharacterId = character.Id, ItemId = item.Id, InventoryItemId = stack.Id, Enchantment = enchantment };
        }

        // a piece the attribute can be on: weapons, armor that takes gems (no hats or looks), rings and amulets; no Legendary
        private static bool IsGear(DataModels.Item item)
        {
            if (item == null || Legendary.IsLegendary(item.Id) || Gems.IsGem(item.Id)) return false;
            var category = (ItemCategory)item.Category;
            if (category != ItemCategory.Weapon && category != ItemCategory.Armor && category != ItemCategory.Ring && category != ItemCategory.Amulet) return false;
            return GemManager.TryGetSlot(item, out _);
        }

        /// <summary>
        ///     Development only: a line "id attribute" or "id gem" in generated-data/substitution-force.txt gives that
        ///     character the find at its next source boss (the line is taken off), so both can be tested without weeks of
        ///     raids. Returns "attribute", "gem" or null.
        /// </summary>
        private static string Forced(Guid characterId)
        {
            try
            {
                if (!string.Equals(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"), "Development", StringComparison.OrdinalIgnoreCase)) return null;
                var path = Path.Combine("generated-data", "substitution-force.txt");
                if (!File.Exists(path)) return null;
                var lines = File.ReadAllLines(path).Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
                var mine = lines.FindIndex(x => Guid.TryParse(x.Split(' ')[0], out var g) && g == characterId);
                if (mine < 0) return null;
                var parts = lines[mine].Split(' ', 2);
                lines.RemoveAt(mine);
                File.WriteAllLines(path, lines);
                var what = parts.Length > 1 ? parts[1].Trim().ToLowerInvariant() : "attribute";
                return what.StartsWith("gem") ? "gem" : "attribute";
            }
            catch
            {
                return null;
            }
        }
    }
}

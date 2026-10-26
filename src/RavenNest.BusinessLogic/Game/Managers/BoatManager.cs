using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Data;
using RavenNest.Models;
using RavenNest.Sessions;

namespace RavenNest.BusinessLogic.Game
{
    /// <summary>
    /// The shipwright on Away (docs/own-boats.md in the game, phase 4): !boat build turns crafted parts, a coin fee and
    /// a Sailing level into a Fishing Boat or a Sloop. Crafted boats are not soulbound (decided 2026-10-26), so they can
    /// be traded and sold; the Rowboat comes from the shipwright's chain instead (QuestManager). The boat is delivered
    /// before the parts and coins are taken, as other purchases do.
    /// </summary>
    public class BoatManager
    {
        public const string ShipwrightIsland = "Away";

        private sealed class Blueprint
        {
            public string Boat;
            public string[] Names;
            public int Sailing, Fishing, Crafting;
            public long Fee;
            public (string Item, long Amount)[] Parts;
        }

        private static readonly Blueprint[] Blueprints =
        {
            new Blueprint
            {
                Boat = "Fishing Boat", Names = new[] { "fishing boat", "fishingboat", "fishing" },
                Sailing = 30, Fishing = 30, Fee = 50_000,
                Parts = new[] { ("Boat Hull", 1L), ("Mast", 1L), ("Sailcloth", 1L), ("Rope", 4L) },
            },
            new Blueprint
            {
                Boat = "Sloop", Names = new[] { "sloop" },
                Sailing = 60, Crafting = 50, Fee = 250_000,
                Parts = new[] { ("Large Hull", 1L), ("Mast", 2L), ("Sailcloth", 3L), ("Rope", 8L) },
            },
        };

        private readonly GameData gameData;
        private readonly PlayerInventoryProvider inventoryProvider;
        private readonly ILogger<BoatManager> logger;
        private readonly object mutex = new object();

        public BoatManager(GameData gameData, PlayerInventoryProvider inventoryProvider, ILogger<BoatManager> logger)
        {
            this.gameData = gameData;
            this.inventoryProvider = inventoryProvider;
            this.logger = logger;
        }

        public BoatBlueprint[] GetBlueprints() => Blueprints.Select(b => new BoatBlueprint
        {
            Boat = b.Boat,
            Sailing = b.Sailing,
            Fishing = b.Fishing,
            Crafting = b.Crafting,
            Fee = b.Fee,
            Parts = b.Parts.Select(p => new BoatPart { Item = p.Item, Amount = p.Amount }).ToArray(),
        }).ToArray();

        public BoatBuildResult Build(SessionToken token, Guid characterId, string boat)
        {
            var character = gameData.GetCharacter(characterId);
            if (character == null || token == null || character.UserIdLock != token.UserId)
                return new BoatBuildResult { Error = "notinsession" };

            var query = (boat ?? "").Trim().ToLowerInvariant();
            var blueprint = Blueprints.FirstOrDefault(b => b.Names.Contains(query) || b.Boat.Equals(query, StringComparison.OrdinalIgnoreCase));
            if (blueprint == null)
                return new BoatBuildResult { Error = "unknown", Blueprints = GetBlueprints() };

            lock (mutex)
            {
                var resources = gameData.GetResources(character);
                var result = new BoatBuildResult { Boat = blueprint.Boat, Fee = blueprint.Fee, Coins = (long)(resources?.Coins ?? 0) };

                var state = gameData.GetCharacterState(character.StateId);
                if (!string.Equals(state?.Island, ShipwrightIsland, StringComparison.OrdinalIgnoreCase))
                {
                    result.Error = "island";
                    result.Island = ShipwrightIsland;
                    return result;
                }

                var skills = gameData.GetCharacterSkills(character.SkillsId);
                foreach (var (skill, level, needed) in new[] { ("Sailing", skills.SailingLevel, blueprint.Sailing), ("Fishing", skills.FishingLevel, blueprint.Fishing), ("Crafting", skills.CraftingLevel, blueprint.Crafting) })
                {
                    if (level >= needed) continue;
                    result.Error = "level";
                    result.Skill = skill;
                    result.Level = needed;
                    return result;
                }

                var inventory = inventoryProvider.Get(character.Id);
                var stacks = inventory.GetUnequippedItems();
                var parts = new List<(DataModels.Item Item, long Amount, List<ReadOnlyInventoryItem> Stacks)>();
                var missing = new List<BoatPart>();
                foreach (var (name, amount) in blueprint.Parts)
                {
                    var item = gameData.GetItems().FirstOrDefault(i => i.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                    var own = item == null ? new List<ReadOnlyInventoryItem>() : stacks.Where(s => s.ItemId == item.Id && !s.Soulbound).ToList();
                    var have = own.Sum(s => s.Amount);
                    if (item == null || have < amount) missing.Add(new BoatPart { Item = name, Amount = amount, Have = have });
                    else parts.Add((item, amount, own));
                }
                if (missing.Count > 0)
                {
                    result.Error = "parts";
                    result.Missing = missing.ToArray();
                    return result;
                }

                if (resources == null || resources.Coins < blueprint.Fee)
                {
                    result.Error = "coins";
                    return result;
                }

                // the boat first: if it cannot be given, nothing is taken
                var boatItem = gameData.GetItems().FirstOrDefault(i => i.Name.Equals(blueprint.Boat, StringComparison.OrdinalIgnoreCase));
                if (boatItem == null || !inventory.TryAddItem(boatItem.Id, 1, out _))
                {
                    logger.LogError("The shipwright could not give " + character.Name + " a " + blueprint.Boat);
                    result.Error = "failed";
                    return result;
                }

                foreach (var (_, amount, own) in parts)
                {
                    var left = amount;
                    foreach (var stack in own)
                    {
                        if (left <= 0) break;
                        var take = Math.Min(stack.Amount, left);
                        if (take > 0 && inventory.RemoveItem(stack, take)) left -= take;
                    }
                }
                resources.Coins -= blueprint.Fee;
                gameData.Touch();
                result.Ok = true;
                result.Coins = (long)resources.Coins;
                return result;
            }
        }
    }
}

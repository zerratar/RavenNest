using System;
using System.Collections.Generic;
using System.Linq;

namespace RavenNest.Combat
{
    /// <summary>Where an egg can be found.</summary>
    public enum EggSource
    {
        /// <summary>A dungeon's reward, by the dungeon's tier (0 to 4).</summary>
        Dungeon = 0,
        Raid = 1,
        /// <summary>A tower floor's chest, by the floor's tier (0 to 4).</summary>
        Tower = 2,
    }

    /// <summary>
    ///     Eggs (the game's docs/battle-pets-design.md, "Eggs and evolution materials"): each hatched species has an egg
    ///     item with a fixed id, found now and then in dungeon, raid and tower rewards. <c>!pets hatch</c> puts one in the
    ///     stable, where it takes a place and warms while its owner plays; it hatches into a level 1 pet once it has had
    ///     its minutes. In the stable an egg is a pet at level 0 whose play time is the minutes it has had.
    /// </summary>
    public static class PetEggs
    {
        /// <summary>The share of a member's minutes that counts: one report a minute, at most this long.</summary>
        public const double MaxMinutesPerReport = 1.5;

        /// <summary>The chance per character of an egg from a dungeon: 3%, and 1% more per tier.</summary>
        public static double DungeonChance(int tier) => 0.03 + 0.01 * Math.Max(0, Math.Min(4, tier));

        /// <summary>The chance per character of an egg from a raid.</summary>
        public const double RaidChance = 0.03;

        /// <summary>The chance per character of an egg in a tower floor's chest.</summary>
        public const double TowerChance = 0.04;

        public static double Chance(EggSource source, int tier) =>
            source == EggSource.Dungeon ? DungeonChance(tier) : source == EggSource.Raid ? RaidChance : TowerChance;

        // where each egg is found: the source, the lowest tier, and how common it is there
        private static readonly (string Species, EggSource Source, int MinTier, double Weight)[] drops =
        {
            ("wolf-pup", EggSource.Dungeon, 0, 10), ("wolf-pup", EggSource.Raid, 0, 10),
            ("bud", EggSource.Dungeon, 0, 10), ("bud", EggSource.Raid, 0, 10),
            ("sun-blossom", EggSource.Dungeon, 0, 6), ("sun-blossom", EggSource.Tower, 0, 6),
            ("practice-dummy", EggSource.Tower, 0, 10),
            ("shell", EggSource.Dungeon, 1, 6), ("shell", EggSource.Raid, 0, 6),
            ("snakelet", EggSource.Dungeon, 2, 5), ("snakelet", EggSource.Raid, 0, 5),
            ("bomb", EggSource.Raid, 0, 4), ("bomb", EggSource.Tower, 1, 4),
            ("dragon-spark", EggSource.Raid, 0, 1), ("dragon-spark", EggSource.Dungeon, 3, 1),
        };

        /// <summary>The eggs a source can give at a tier, with their weights.</summary>
        public static IEnumerable<(PetSpecies Species, double Weight)> Table(EggSource source, int tier) =>
            drops.Where(d => d.Source == source && tier >= d.MinTier)
                 .Select(d => (BattlePets.GetSpecies(d.Species), d.Weight))
                 .Where(d => d.Item1 != null);

        /// <summary>The egg a roll (0 to 1) picks from a source's table, or null when it has none.</summary>
        public static PetSpecies Pick(EggSource source, int tier, double roll)
        {
            var table = Table(source, tier).ToList();
            var total = table.Sum(t => t.Weight);
            if (total <= 0) return null;
            var at = Math.Max(0, Math.Min(0.999999, roll)) * total;
            foreach (var (species, weight) in table)
            {
                if (at < weight) return species;
                at -= weight;
            }
            return table[table.Count - 1].Species;
        }

        /// <summary>Every species that hatches from an egg.</summary>
        public static IEnumerable<PetSpecies> Species => BattlePets.Species.Where(s => s.Hatches);

        /// <summary>The egg item's fixed id, so the game and the website know an egg from its id alone.</summary>
        public static Guid ItemId(string speciesId) => Gems.NameGuid("ravenfall-pet-egg-" + (speciesId ?? "").Trim().ToLowerInvariant());

        private static Dictionary<Guid, PetSpecies> byItem;

        /// <summary>The species an egg item hatches into, or null when the item is not an egg.</summary>
        public static PetSpecies SpeciesForEgg(Guid itemId)
        {
            if (byItem == null) byItem = Species.ToDictionary(s => ItemId(s.Id));
            return byItem.TryGetValue(itemId, out var s) ? s : null;
        }

        public static bool IsEgg(Guid itemId) => SpeciesForEgg(itemId) != null;

        /// <summary>An egg by what people call it: "wolf pup egg", "wolf", "bud seed", "dragon".</summary>
        public static PetSpecies FindEgg(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            var t = text.Trim().ToLowerInvariant();
            return Species.FirstOrDefault(s => s.EggName.ToLowerInvariant() == t || s.Id == t)
                ?? Species.FirstOrDefault(s => s.StageNames.Any(n => n.ToLowerInvariant() == t))
                ?? Species.FirstOrDefault(s => s.EggName.ToLowerInvariant().Contains(t) || s.StageNames.Any(n => n.ToLowerInvariant().Contains(t)));
        }

        /// <summary>The minutes an egg needs, with the owner's clan's Rookery (up to a quarter faster).</summary>
        public static double HatchMinutes(PetSpecies species, int rookeryLevel) =>
            species == null ? 0 : Math.Max(1, species.HatchMinutes * (1 - Rookery.HatchSpeedup(rookeryLevel)));

        /// <summary>A pet in the stable that is still an egg.</summary>
        public static bool IsUnhatched(int level) => level <= 0;
    }

    /// <summary>
    ///     What evolving costs besides levels: raw gems of the pet's own kind (the game's docs/battle-pets-design.md,
    ///     "Eggs and evolution materials"). An element's gem for an elemental pet (Ruby fire, Sapphire ice, Topaz
    ///     lightning, Emerald poison); a physical pet takes its role's gem (Diamond for damage, Onyx for tanks and
    ///     gatherers, Amethyst for healers and support). Raw gems are mined and traded, so nobody needs a clan for them;
    ///     the clan's Rookery makes it cheaper.
    /// </summary>
    public static class PetEvolution
    {
        /// <summary>Raw gems for the first and the second evolution.</summary>
        public static readonly int[] GemsPerStage = { 10, 30 };

        public static GemKind GemFor(PetSpecies species)
        {
            if (species == null) return GemKind.Diamond;
            switch (species.Element)
            {
                case Element.Fire: return GemKind.Ruby;
                case Element.Ice: return GemKind.Sapphire;
                case Element.Lightning: return GemKind.Topaz;
                case Element.Poison: return GemKind.Emerald;
            }
            switch (species.Role)
            {
                case PetRole.Damage: return GemKind.Diamond;
                case PetRole.Healer:
                case PetRole.Support: return GemKind.Amethyst;
                default: return GemKind.Onyx;
            }
        }

        /// <summary>The raw gems the evolution from <paramref name="stage"/> takes, with the owner's clan's Rookery.</summary>
        public static int GemsFor(int stage, int rookeryLevel)
        {
            if (stage < 0 || stage >= GemsPerStage.Length) return 0;
            return Math.Max(1, (int)Math.Ceiling(GemsPerStage[stage] * (1 - Rookery.EvolutionDiscount(rookeryLevel))));
        }

        /// <summary>"10 Ruby", for the missing part of an answer.</summary>
        public static string Describe(PetSpecies species, int stage, int rookeryLevel) =>
            GemsFor(stage, rookeryLevel) + " " + Gems.RawName(GemFor(species));
    }
}

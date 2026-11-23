using System;
using System.Collections.Generic;
using System.Linq;
using RavenNest.Combat;
using RavenNest.Models;

namespace RavenNest.BusinessLogic.Game
{
    /// <summary>
    ///     The Message in a Bottle and the Treasure Map (v0.9.7, the game's docs/own-boats.md, "Messages in bottles"):
    ///     the rolls, the island a map marks and what a dug up treasure holds. Only numbers and choices here; the
    ///     <see cref="BottleManager"/> takes the items and gives the rewards, and the game has the words.
    /// </summary>
    /// <remarks>
    ///     A bottle (2% a fishing round at sea, an hour apart) holds a note most of the time, and about one in eight a
    ///     Treasure Map. A note is a number: the game words it (Localization, UI_BOTTLE_NOTE_n), so it can be translated.
    ///     A map keeps its island in the inventory item's tag ("Kyo", ten characters at most) and is dug up on that
    ///     island with !use treasure map.
    /// </remarks>
    public static class Bottles
    {
        public const string BottleName = "Message in a Bottle";
        public const string MapName = "Treasure Map";

        /// <summary>About one bottle in eight holds a map instead of a note.</summary>
        public const double MapChance = 1.0 / 8;

        /// <summary>
        ///     The ordinary notes: UI_BOTTLE_NOTE_1 to UI_BOTTLE_NOTE_48 in the game, all as likely. The game must have
        ///     this many (one it lacks reads as its first note).
        /// </summary>
        public const int NoteCount = 48;

        /// <summary>The rare notes (UI_BOTTLE_NOTE_101 and on): together this share of the notes, all as likely.</summary>
        public const double RareNoteChance = 0.08;

        /// <summary>A note that is only a drawing of a raven.</summary>
        public const int RavenDrawing = 101;
        /// <summary>A note addressed to the very player who found it, by name.</summary>
        public const int AddressedToYou = 102;
        /// <summary>"This is not a treasure map." And then nothing.</summary>
        public const int NotATreasureMap = 103;
        /// <summary>Soaked through: only the word "bottles" can still be read.</summary>
        public const int Soggy = 104;
        /// <summary>A smaller bottle inside the bottle, with a smaller note inside that.</summary>
        public const int SmallerBottle = 105;

        public static readonly int[] RareNotes = { RavenDrawing, AddressedToYou, NotATreasureMap, Soggy, SmallerBottle };

        /// <summary>The islands a map can mark: the ones with ground to dig. Never the sea, Ravebridge (an event island) or the ferry.</summary>
        public static readonly Island[] MapIslands =
        {
            Island.Home, Island.Away, Island.Ironhill, Island.Kyo, Island.Heim, Island.Atria, Island.Eldara, Island.Mangrove,
        };

        /// <summary>The Mangrove island is where pirates bury things: twice as likely as another island, where it is open.</summary>
        public const double MangroveWeight = 2;

        // ---- Opening a bottle ----------------------------------------------------------------------------------

        /// <summary>What a bottle holds: a map, or the number of the note.</summary>
        public readonly struct Contents
        {
            public readonly bool Map;
            public readonly int Note;
            public Contents(bool map, int note) { Map = map; Note = note; }
        }

        public static Contents Open(Random rng) => Open(rng.NextDouble(), rng.NextDouble(), rng.NextDouble());

        /// <summary>From three rolls (0 to 1): the map's, the rare note's and the one that picks the note.</summary>
        public static Contents Open(double mapRoll, double rareRoll, double pickRoll)
        {
            if (mapRoll < MapChance) return new Contents(true, 0);
            var pick = Math.Max(0, Math.Min(0.999999, pickRoll));
            if (rareRoll < RareNoteChance) return new Contents(false, RareNotes[(int)(pick * RareNotes.Length)]);
            return new Contents(false, 1 + (int)(pick * NoteCount));
        }

        /// <summary>Whether a number is a note the server can hand out.</summary>
        public static bool IsNote(int note) => (note >= 1 && note <= NoteCount) || Array.IndexOf(RareNotes, note) >= 0;

        // ---- The island a map marks ----------------------------------------------------------------------------

        // the islands' level requirements as the resource tasks keep them (ResourceTaskProcessor.islandLevelRequirements)
        private static readonly Lazy<Processors.Tasks.ResourceTaskProcessor> requirements =
            new Lazy<Processors.Tasks.ResourceTaskProcessor>(() => new Processors.Tasks.FishingTaskProcessor());

        /// <summary>
        ///     Whether a character can work on an island by the server's requirements: any of the skills it lists at its
        ///     level (Woodcutting, Mining, Fishing, Farming or Gathering). The Mangrove island only where the stream has
        ///     opened it.
        /// </summary>
        public static bool CanReach(Island island, Func<Skill, int> levelOf, bool mangroveOpen)
        {
            if (Array.IndexOf(MapIslands, island) < 0) return false;
            if (island == Island.Mangrove && !mangroveOpen) return false;
            if (!requirements.Value.islandLevelRequirements.TryGetValue(island, out var table)) return false;
            foreach (var kv in table)
            {
                if (levelOf(kv.Key) >= kv.Value) return true;
            }
            return false;
        }

        /// <summary>The islands a character's map can mark, in <see cref="MapIslands"/> order. Home at the least.</summary>
        public static List<Island> Reachable(Func<Skill, int> levelOf, bool mangroveOpen)
        {
            var list = MapIslands.Where(i => CanReach(i, levelOf, mangroveOpen)).ToList();
            if (list.Count == 0) list.Add(Island.Home);
            return list;
        }

        /// <summary>One of the islands, from a roll (0 to 1): all as likely, the Mangrove island twice as likely.</summary>
        public static Island PickIsland(IReadOnlyList<Island> islands, double roll)
        {
            if (islands == null || islands.Count == 0) return Island.Home;
            var total = islands.Sum(Weight);
            var at = Math.Max(0, Math.Min(0.999999, roll)) * total;
            foreach (var island in islands)
            {
                if (at < Weight(island)) return island;
                at -= Weight(island);
            }
            return islands[islands.Count - 1];
        }

        private static double Weight(Island island) => island == Island.Mangrove ? MangroveWeight : 1;

        /// <summary>The island a map's tag names ("Kyo"), or None for a map without one.</summary>
        public static Island IslandOf(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag) || !Enum.TryParse<Island>(tag.Trim(), true, out var island)) return Island.None;
            return Array.IndexOf(MapIslands, island) >= 0 ? island : Island.None;
        }

        /// <summary>The tag a map keeps: the island's name, ten characters at most.</summary>
        public static string TagOf(Island island) => island.ToString();

        // ---- What a treasure holds -----------------------------------------------------------------------------

        /// <summary>A gem now and then.</summary>
        public const double GemChance = 0.25;
        /// <summary>A pet egg from the pirates' table (before the animal eggs widen it, as every egg source is).</summary>
        public const double EggChance = 0.05;
        /// <summary>Another bottle, of course.</summary>
        public const double BottleChance = 0.08;
        /// <summary>Something to remember: the Treasure Hunter title, or a boat flag or figurehead from the treasure set.</summary>
        public const double MemorableChance = 0.02;

        /// <summary>The rolls for one treasure; the manager gives them.</summary>
        public sealed class Treasure
        {
            public long Coins;
            /// <summary>A gem, or null.</summary>
            public (GemKind Kind, GemTier Tier)? Gem;
            public bool Egg;
            public bool Bottle;
            public bool Memorable;
        }

        /// <summary>
        ///     Coins by the character's level (the highest of its gathering skills, 1 to 999): (400 + 12 a level), times
        ///     1 to 2, to the nearest 10. Level 1 finds 410 to 820, level 100 1,600 to 3,200, level 999 12,390 to 24,780.
        /// </summary>
        public static long Coins(int level, double roll)
        {
            level = Math.Max(1, Math.Min(GameMath.MaxLevel, level));
            return (long)Math.Round((400 + 12.0 * level) * (1 + Math.Max(0, Math.Min(1, roll))) / 10) * 10;
        }

        /// <summary>A gem's tier by level, from a roll: Chipped below 300; from 300 a Flawed one in three; from 700 Polished one in ten and Flawed four in ten.</summary>
        public static GemTier GemTierFor(int level, double roll)
        {
            if (level >= 700) return roll < 0.1 ? GemTier.Polished : roll < 0.5 ? GemTier.Flawed : GemTier.Chipped;
            if (level >= 300) return roll < 0.3 ? GemTier.Flawed : GemTier.Chipped;
            return GemTier.Chipped;
        }

        public static Treasure Roll(int level, Random rng)
        {
            var treasure = new Treasure { Coins = Coins(level, rng.NextDouble()) };
            if (rng.NextDouble() < GemChance)
            {
                treasure.Gem = ((GemKind)rng.Next(Gems.KindCount), GemTierFor(level, rng.NextDouble()));
            }
            treasure.Egg = rng.NextDouble() < Math.Min(1.0, EggChance * PetEggs.Widened(EggSource.PirateRaid, 0));
            treasure.Bottle = rng.NextDouble() < BottleChance;
            treasure.Memorable = rng.NextDouble() < MemorableChance;
            return treasure;
        }

        /// <summary>The level treasure is scaled by: the highest of the gathering skills (the ones the islands ask for).</summary>
        public static int LevelOf(Func<Skill, int> levelOf)
        {
            return Math.Max(1, new[] { Skill.Woodcutting, Skill.Mining, Skill.Fishing, Skill.Farming, Skill.Gathering }.Max(levelOf));
        }
    }
}

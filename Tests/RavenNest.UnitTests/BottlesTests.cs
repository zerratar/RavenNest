using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RavenNest.BusinessLogic.Game;
using RavenNest.Combat;
using RavenNest.Models;

namespace RavenNest.UnitTests
{
    /// <summary>The Message in a Bottle and the Treasure Map: the rolls and the island a map marks (Bottles).</summary>
    [TestClass]
    public class BottlesTests
    {
        [TestMethod]
        public void Open_LowMapRoll_IsAMap()
        {
            Assert.IsTrue(Bottles.Open(0.0, 0.5, 0.5).Map);
            Assert.IsTrue(Bottles.Open(Bottles.MapChance - 0.0001, 0.5, 0.5).Map);
            Assert.IsFalse(Bottles.Open(Bottles.MapChance, 0.5, 0.5).Map);
        }

        [TestMethod]
        public void Open_EveryPickRoll_GivesANoteTheGameHas()
        {
            var seen = new HashSet<int>();
            for (var i = 0; i <= 1000; ++i)
            {
                var pick = i / 1000.0;
                var common = Bottles.Open(0.9, 0.9, pick);
                var rare = Bottles.Open(0.9, 0.0, pick);
                Assert.IsFalse(common.Map);
                Assert.IsTrue(common.Note >= 1 && common.Note <= Bottles.NoteCount, "common note " + common.Note);
                Assert.IsTrue(Bottles.RareNotes.Contains(rare.Note), "rare note " + rare.Note);
                Assert.IsTrue(Bottles.IsNote(common.Note) && Bottles.IsNote(rare.Note));
                seen.Add(common.Note);
                seen.Add(rare.Note);
            }
            // every note can come up
            Assert.AreEqual(Bottles.NoteCount + Bottles.RareNotes.Length, seen.Count);
        }

        [TestMethod]
        public void Open_ManyBottles_ShareOfMapsAndRareNotesAsDesigned()
        {
            var rng = new Random(1234);
            int maps = 0, rare = 0, n = 200_000;
            for (var i = 0; i < n; ++i)
            {
                var c = Bottles.Open(rng);
                if (c.Map) maps++;
                else if (c.Note > Bottles.NoteCount) rare++;
            }
            Assert.AreEqual(1.0 / 8, maps / (double)n, 0.005);
            Assert.AreEqual((1 - 1.0 / 8) * Bottles.RareNoteChance, rare / (double)n, 0.004);
        }

        [TestMethod]
        public void Reachable_ByTheHighestOfTheGatheringSkills()
        {
            // a new character: Home only (the Mangrove island where the stream has opened it)
            CollectionAssert.AreEqual(new[] { Island.Home }, Bottles.Reachable(s => 1, false));
            CollectionAssert.AreEqual(new[] { Island.Home, Island.Mangrove }, Bottles.Reachable(s => 1, true));

            // Fishing 210 and everything else low: up to Kyo (200), not Heim (300)
            var islands = Bottles.Reachable(s => s == Skill.Fishing ? 210 : 5, false);
            CollectionAssert.AreEqual(new[] { Island.Home, Island.Away, Island.Ironhill, Island.Kyo }, islands);

            // combat does not open an island: the islands ask for the gathering skills
            CollectionAssert.AreEqual(new[] { Island.Home }, Bottles.Reachable(s => s == Skill.Attack ? 999 : 1, false));

            // the top: every island with ground, never the sea, Ravebridge or the ferry
            var all = Bottles.Reachable(s => 999, true);
            CollectionAssert.AreEqual(Bottles.MapIslands, all);
            Assert.IsFalse(all.Contains(Island.Sea) || all.Contains(Island.Ravebridge) || all.Contains(Island.Ferry));
        }

        [TestMethod]
        public void Reachable_ExactlyAtTheRequirement()
        {
            Assert.IsTrue(Bottles.CanReach(Island.Eldara, s => s == Skill.Mining ? 700 : 1, false));
            Assert.IsFalse(Bottles.CanReach(Island.Eldara, s => s == Skill.Mining ? 699 : 1, false));
            Assert.IsTrue(Bottles.CanReach(Island.Away, s => s == Skill.Gathering ? 50 : 1, false));
            Assert.IsFalse(Bottles.CanReach(Island.Mangrove, s => 999, false));
        }

        [TestMethod]
        public void PickIsland_MangroveTwiceAsLikely_OthersAlike()
        {
            var islands = new List<Island> { Island.Home, Island.Away, Island.Mangrove };
            var counts = new Dictionary<Island, int>();
            const int steps = 40_000;
            for (var i = 0; i < steps; ++i)
            {
                var island = Bottles.PickIsland(islands, (i + 0.5) / steps);
                counts[island] = counts.TryGetValue(island, out var c) ? c + 1 : 1;
            }
            Assert.AreEqual(0.25, counts[Island.Home] / (double)steps, 0.001);
            Assert.AreEqual(0.25, counts[Island.Away] / (double)steps, 0.001);
            Assert.AreEqual(0.5, counts[Island.Mangrove] / (double)steps, 0.001);
            Assert.AreEqual(Island.Home, Bottles.PickIsland(islands, 0));
            Assert.AreEqual(Island.Mangrove, Bottles.PickIsland(islands, 1));
            Assert.AreEqual(Island.Home, Bottles.PickIsland(new List<Island>(), 0.5));
        }

        [TestMethod]
        public void MapTag_RoundTripsAndFitsTheColumn()
        {
            foreach (var island in Bottles.MapIslands)
            {
                var tag = Bottles.TagOf(island);
                Assert.IsTrue(tag.Length <= 10, tag);
                Assert.AreEqual(island, Bottles.IslandOf(tag));
                Assert.AreEqual(island, Bottles.IslandOf(tag.ToLowerInvariant()));
            }
            Assert.AreEqual(Island.None, Bottles.IslandOf(null));
            Assert.AreEqual(Island.None, Bottles.IslandOf(""));
            Assert.AreEqual(Island.None, Bottles.IslandOf("Ravebridge"));
            Assert.AreEqual(Island.None, Bottles.IslandOf("Sea"));
            Assert.AreEqual(Island.None, Bottles.IslandOf("flag_8"));
        }

        [TestMethod]
        public void Coins_ScaleWithLevel_ToTheNearestTen()
        {
            Assert.AreEqual(410, Bottles.Coins(1, 0));
            Assert.AreEqual(820, Bottles.Coins(1, 1));
            Assert.AreEqual(1600, Bottles.Coins(100, 0));
            Assert.AreEqual(3200, Bottles.Coins(100, 1));
            Assert.AreEqual(12390, Bottles.Coins(999, 0));
            Assert.AreEqual(24780, Bottles.Coins(999, 1));
            Assert.AreEqual(Bottles.Coins(999, 1), Bottles.Coins(5000, 1));
            Assert.AreEqual(0, Bottles.Coins(357, 0.37) % 10);
        }

        [TestMethod]
        public void GemTier_GrowsWithLevel()
        {
            for (var r = 0.0; r < 1; r += 0.01) Assert.AreEqual(GemTier.Chipped, Bottles.GemTierFor(299, r));
            Assert.AreEqual(GemTier.Flawed, Bottles.GemTierFor(300, 0.29));
            Assert.AreEqual(GemTier.Chipped, Bottles.GemTierFor(300, 0.3));
            Assert.AreEqual(GemTier.Polished, Bottles.GemTierFor(700, 0.05));
            Assert.AreEqual(GemTier.Flawed, Bottles.GemTierFor(999, 0.4));
            Assert.AreEqual(GemTier.Chipped, Bottles.GemTierFor(999, 0.6));
        }

        [TestMethod]
        public void Roll_ManyTreasures_OddsAsDesigned()
        {
            var rng = new Random(42);
            int n = 200_000, gems = 0, eggs = 0, bottles = 0, memorable = 0;
            long least = long.MaxValue, most = 0;
            for (var i = 0; i < n; ++i)
            {
                var t = Bottles.Roll(500, rng);
                if (t.Gem != null) gems++;
                if (t.Egg) eggs++;
                if (t.Bottle) bottles++;
                if (t.Memorable) memorable++;
                least = Math.Min(least, t.Coins);
                most = Math.Max(most, t.Coins);
            }
            Assert.AreEqual(Bottles.GemChance, gems / (double)n, 0.005);
            Assert.AreEqual(Bottles.EggChance * PetEggs.Widened(EggSource.PirateRaid, 0), eggs / (double)n, 0.003);
            Assert.AreEqual(Bottles.BottleChance, bottles / (double)n, 0.003);
            Assert.AreEqual(Bottles.MemorableChance, memorable / (double)n, 0.002);
            Assert.IsTrue(least >= Bottles.Coins(500, 0) && most <= Bottles.Coins(500, 1));
        }

        [TestMethod]
        public void LevelOf_IsTheHighestGatheringSkill()
        {
            Assert.AreEqual(1, Bottles.LevelOf(s => 0));
            Assert.AreEqual(420, Bottles.LevelOf(s => s == Skill.Farming ? 420 : s == Skill.Attack ? 999 : 10));
        }
    }
}

using RavenNest.Combat;
using Xunit;

namespace RavenNest.Combat.Tests
{
    /// <summary>Taming gear (the game's docs/taming-gear-design.md): the tame chance stat and the tower's Beastmaster pieces.</summary>
    public class TamingGearTests
    {
        [Fact]
        public void TameChance_IsAShare_ReadAndWritten()
        {
            var stats = GearStats.Parse("TAME_CHANCE:15%;PET_POWER:2.3%");
            Assert.Equal(0.15, stats[GearStat.TameChance], 6);
            Assert.Equal(0.023, stats[GearStat.PetPower], 6);
            Assert.True(GearStats.IsShare(GearStat.TameChance));
            Assert.Equal("Tame Chance", GearStats.Label(GearStat.TameChance));
            Assert.Equal(0.15, GearStats.Parse(stats.ToString())[GearStat.TameChance], 6);
            // the stats before it keep their numbers: saved texts name them, but the values array is in this order
            Assert.Equal(GearStat.PetSafeGathering + 1, GearStat.TameChance);
            Assert.Equal(GearStat.TameChance + 1, GearStat.ThornsAllStyles); // the Legendaries (2026-11-23) come after it
            Assert.Equal(GearStat.ThornsAllStyles + 1, GearStat.Substitution); // Substitution (2026-11-27) after them
            Assert.Equal((int)GearStat.Substitution + 1, GearStats.Count);
        }

        [Fact]
        public void TameChance_IsAShareMoreOfTheChance_NotPointsAdded()
        {
            var species = BattlePets.Archetypes[0];
            var level = species.TameLevel + 10;
            var plain = Taming.Chance(level, species);
            Assert.True(plain > 0);
            Assert.Equal(plain * 1.15, Taming.Chance(level, species, false, 0.15), 9);
            // after the treat
            Assert.Equal(plain * Taming.TreatMultiplier * 1.15, Taming.Chance(level, species, true, 0.15), 9);
            // nothing below the creature's level, gear or not; a negative bonus counts as none
            Assert.Equal(0, Taming.Chance(species.TameLevel - 1, species, true, 0.15));
            Assert.Equal(plain, Taming.Chance(level, species, false, -0.5), 9);
        }

        [Fact]
        public void Tower_BeastmasterPiece_OnlyWithAPetOut_OneInFive()
        {
            Assert.Equal(0.2, TowerLoot.BeastmasterChance, 9);
            Assert.False(TowerLoot.Beastmaster(false, 0.0));
            Assert.True(TowerLoot.Beastmaster(true, 0.0));
            Assert.True(TowerLoot.Beastmaster(true, 0.1999));
            Assert.False(TowerLoot.Beastmaster(true, 0.2));
            Assert.False(TowerLoot.Beastmaster(true, 0.99));
        }
    }
}

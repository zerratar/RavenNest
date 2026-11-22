using Microsoft.VisualStudio.TestTools.UnitTesting;
using RavenNest.Combat;
using RavenNest.Models;
using System.Linq;

namespace RavenNest.UnitTests
{
    /// <summary>Taming gear, the Beastmaster line (the game's docs/taming-gear-design.md): its items, the fit rule and its stats.</summary>
    [TestClass]
    public class TamingGearTests
    {
        [TestMethod]
        public void Materials_ComeAfterEveryOtherLine_AndNothingMoved()
        {
            // saved items hold the numbers: the twelve lines before keep theirs
            Assert.AreEqual((int)ItemMaterial.Apprentice + 12 * ItemLines.Tiers - 1, (int)ItemMaterial.Celestial);
            Assert.AreEqual((int)ItemMaterial.Celestial + 1, (int)ItemMaterial.Fledgling);
            Assert.AreEqual((int)ItemMaterial.Celestial + 8, (int)ItemMaterial.Wyrmkin);

            var names = new[] { ItemMaterial.Fledgling, ItemMaterial.Trapper, ItemMaterial.Houndmaster, ItemMaterial.Falconer, ItemMaterial.Wildheart, ItemMaterial.Packlord, ItemMaterial.Primal, ItemMaterial.Wyrmkin };
            for (var tier = 1; tier <= ItemLines.Tiers; ++tier)
            {
                Assert.AreEqual(names[tier - 1], ItemLines.Material(ItemLine.Beastmaster, tier));
                Assert.AreEqual(ItemLine.Beastmaster, ItemLines.Of(names[tier - 1]));
                Assert.AreEqual(tier, ItemLines.TierOf(names[tier - 1]));
                Assert.IsTrue(ItemLines.IsClassGear(names[tier - 1]));
            }
            // the Ranger's second tier is the Tracker: the spec's name for the second tier was taken
            Assert.AreEqual(ItemLine.Ranger, ItemLines.Of(ItemMaterial.Tracker));
            Assert.AreEqual(13, ItemLines.Count);
            Assert.AreEqual(0.6, ItemLines.ArmorFactor(ItemLine.Beastmaster), 1e-9);
        }

        [TestMethod]
        public void FitsTamingOnly_SoEquipBestForACombatSkillNeverPicksIt()
        {
            Assert.AreEqual(ItemLines.Bit(ItemLine.Beastmaster), ItemLines.Fits(Skill.Taming));
            foreach (var skill in new[] { Skill.Attack, Skill.Strength, Skill.Defense, Skill.Ranged, Skill.Magic, Skill.Healing, Skill.Health, Skill.Fishing })
                Assert.IsFalse(ItemLines.Fits(ItemLine.Beastmaster, skill), skill.ToString());
            foreach (var argument in new[] { "Attack", "Strength", "Defense", "Ranged", "Magic", "Healing", "All" })
                Assert.IsFalse(ItemLines.Has(GearScore.FitsFor("Fighting", argument), ItemLine.Beastmaster), argument);

            // training Attack, a Packlord Hide Vest (tier 6, 60% of plate's armor) counts as its own armor and loses
            // to the plate chest of its level; a Warlord Hauberk (Melee, fits) beats both
            var fits = GearScore.FitsFor(Skill.Attack, ItemMaterial.None);
            var plateChest = 139;
            var vest = (int)System.Math.Ceiling(plateChest * 0.6);
            Assert.AreEqual(vest, GearScore.Value(ItemMaterial.Packlord, vest, fits));
            Assert.IsFalse(GearScore.Beats(ItemMaterial.Packlord, vest, ItemMaterial.ElderBronze, plateChest, fits));
            // but worn, with nothing trained that names a line, it is kept (what a player wears is their choice)
            Assert.IsTrue(ItemLines.Has(GearScore.FitsFor(Skill.None, ItemMaterial.Packlord), ItemLine.Beastmaster));
        }

        [TestMethod]
        public void NeedsTaming_AtTheTiersLevel()
        {
            Assert.AreEqual(1, ItemLines.TamingLevelFor(ItemMaterial.Fledgling, 1));
            Assert.AreEqual(120, ItemLines.TamingLevelFor(ItemMaterial.Falconer, 120));
            Assert.AreEqual(900, ItemLines.TamingLevelFor(ItemMaterial.Wyrmkin, 900));
            Assert.AreEqual(1, ItemLines.TamingLevelFor(ItemMaterial.Fledgling, 0)); // never "no requirement"
            Assert.AreEqual(0, ItemLines.TamingLevelFor(ItemMaterial.Rune, 70));
            Assert.AreEqual(0, ItemLines.TamingLevelFor(ItemMaterial.Hawkeye, 900));
            Assert.AreEqual(0, ItemLines.TamingLevelFor(ItemMaterial.None, 1));

            // the server's rule
            var vest = new DataModels.Item { Material = (int)ItemMaterial.Falconer, Level = 120, Category = (int)DataModels.ItemCategory.Armor };
            var skills = new DataModels.Skills();
            skills.TamingLevel = 119;
            Assert.IsFalse(RavenNest.BusinessLogic.Game.PlayerInventory.CanEquipItem(vest, skills));
            skills.TamingLevel = 120;
            Assert.IsTrue(RavenNest.BusinessLogic.Game.PlayerInventory.CanEquipItem(vest, skills));
        }

        [TestMethod]
        public void EquipWords_AreTheLine()
        {
            foreach (var word in new[] { "beastmaster", "Beastmaster", "tamer", "taming", "beastmaster line", "Beast Master" })
            {
                var outfit = GearOutfit.Parse(word);
                Assert.IsNotNull(outfit, word);
                Assert.IsTrue(outfit.OnlyLine, word);
                Assert.AreEqual(ItemLine.Beastmaster, outfit.Line, word);
                Assert.AreEqual(ItemLines.Bit(ItemLine.Beastmaster), outfit.Fits, word);
                Assert.AreEqual(ItemLine.Beastmaster, GearOutfit.Parse(outfit.Word).Line, word);
            }
            Assert.IsTrue(GearOutfit.Parse("tamer").Covers(ItemMaterial.Primal));
            Assert.IsFalse(GearOutfit.Parse("tamer").Covers(ItemMaterial.Hawkeye));
            Assert.IsFalse(GearOutfit.Parse("ranger").Covers(ItemMaterial.Primal));
            Assert.AreEqual("beastmaster", GearOutfit.NameOf(ItemLine.Beastmaster));
        }

        private static GearStats PieceStats(int tier, DataModels.ItemType type)
        {
            var method = typeof(RavenNest.BusinessLogic.Data.GameData).GetMethod("ClassGearStats", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            return GearStats.Parse((string)method.Invoke(null, new object[] { ItemLine.Beastmaster, tier, type }));
        }

        [TestMethod]
        public void Stats_FivePiecesAddUpToTheSet()
        {
            foreach (var (tier, power, crit, health, tame, gathering) in new[] { (1, 0.04, 0.005, 0.01, 0.02, 0.01), (8, 0.75, 0.03, 0.08, 0.15, 0.10) })
            {
                var set = new GearStats();
                foreach (var type in new[] { DataModels.ItemType.Hat, DataModels.ItemType.Chest, DataModels.ItemType.Gloves, DataModels.ItemType.Leggings, DataModels.ItemType.Boots })
                    set.Add(PieceStats(tier, type));
                Assert.AreEqual(power, set[GearStat.PetPower], 1e-9, "pet power, tier " + tier);
                Assert.AreEqual(crit, set[GearStat.CritChance], 1e-9, "crit, tier " + tier);
                Assert.AreEqual(health, set[GearStat.MaxHealthBonus], 1e-9, "health, tier " + tier);
                Assert.AreEqual(tame, set[GearStat.TameChance], 1e-9, "tame chance, tier " + tier);
                Assert.AreEqual(gathering, set[GearStat.PetGathering], 1e-9, "gathering, tier " + tier);
                // no style power: that is the price of the pet's numbers
                Assert.AreEqual(0, set[GearStat.MeleePowerBonus] + set[GearStat.RangedPowerBonus] + set[GearStat.MagicPowerBonus]);
            }
            // the two head pieces have the same stats
            Assert.AreEqual(PieceStats(5, DataModels.ItemType.Hat).ToString(), PieceStats(5, DataModels.ItemType.HeadCovering).ToString());
        }

        [TestMethod]
        public void Items_SixPiecesATier_WithFixedIds()
        {
            var slots = RavenNest.BusinessLogic.Data.GameData.ClassGearSlots(ItemLine.Beastmaster, 4);
            Assert.AreEqual(5, slots.Count);
            Assert.AreEqual(2, slots[0].Length); // two head pieces
            Assert.IsTrue(RavenNest.BusinessLogic.Data.GameData.ClassGearLinesMade.Contains(ItemLine.Beastmaster) == true);
            Assert.AreEqual(RavenNest.BusinessLogic.Data.GameData.ClassGearItemId(ItemLine.Beastmaster, 4, "chest"), slots[1][0]);
        }
    }
}

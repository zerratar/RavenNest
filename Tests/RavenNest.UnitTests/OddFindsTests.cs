using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RavenNest.BusinessLogic.Game;
using RavenNest.Models;

namespace RavenNest.UnitTests
{
    /// <summary>Odd finds while gathering (OddFinds): what each skill turns up, how rare, and the wait between two.</summary>
    [TestClass]
    public class OddFindsTests
    {
        [TestMethod]
        public void Pick_EveryGatheringSkill_FindsSomething()
        {
            foreach (var skill in new[] { Skill.Woodcutting, Skill.Mining, Skill.Fishing, Skill.Farming, Skill.Gathering })
            {
                var name = OddFinds.Pick(skill, Island.Home, 0.9);
                Assert.IsNotNull(name, skill.ToString());
                CollectionAssert.Contains(OddFinds.Names, name);
            }
            Assert.IsNull(OddFinds.Pick(Skill.Attack, Island.Home, 0.1));
        }

        [TestMethod]
        public void Pick_Fishing_QuackersOnlyOffKyo()
        {
            Assert.AreEqual(OddFinds.Quackers, OddFinds.Pick(Skill.Fishing, Island.Kyo, 0.1));
            Assert.AreEqual(OddFinds.OldBoot, OddFinds.Pick(Skill.Fishing, Island.Kyo, 0.9));
            foreach (var island in new[] { Island.Home, Island.Away, Island.Heim, Island.None, Island.Mangrove })
                for (var roll = 0.0; roll < 1.0; roll += 0.05)
                    Assert.AreEqual(OddFinds.OldBoot, OddFinds.Pick(Skill.Fishing, island, roll), island + " " + roll);
        }

        [TestMethod]
        public void Rolls_IsRare()
        {
            Assert.IsTrue(OddFinds.Rolls(0.0));
            Assert.IsFalse(OddFinds.Rolls(OddFinds.ChancePerRound));
            Assert.IsFalse(OddFinds.Rolls(0.5));
            // a round every 10 seconds: no more than one in a few hours of gathering on average
            var hours = 10.0 / OddFinds.ChancePerRound / 3600.0;
            Assert.IsTrue(hours >= 4, "about one in " + hours + " hours");
        }

        [TestMethod]
        public void TryTake_WaitsBetweenTwoFinds()
        {
            OddFinds.Reset();
            var a = Guid.NewGuid();
            var b = Guid.NewGuid();
            var now = new DateTime(2026, 11, 23, 12, 0, 0, DateTimeKind.Utc);
            Assert.IsTrue(OddFinds.TryTake(a, now));
            Assert.IsFalse(OddFinds.TryTake(a, now.AddHours(1)));
            Assert.IsTrue(OddFinds.TryTake(b, now.AddHours(1)), "each character has its own wait");
            Assert.IsFalse(OddFinds.TryTake(a, now + OddFinds.Wait - TimeSpan.FromSeconds(1)));
            Assert.IsTrue(OddFinds.TryTake(a, now + OddFinds.Wait));
            Assert.IsFalse(OddFinds.TryTake(a, now + OddFinds.Wait + TimeSpan.FromMinutes(1)), "the clock started again");
            OddFinds.Reset();
        }

        [TestMethod]
        public void Names_AreStoredNames()
        {
            // item names hold no apostrophes (GameData.ItemName), and every one is different
            Assert.IsTrue(OddFinds.Names.All(n => n.IndexOf('\'') < 0));
            Assert.AreEqual(OddFinds.Names.Length, OddFinds.Names.Distinct().Count());
        }
    }
}

using Microsoft.VisualStudio.TestTools.UnitTesting;
using RavenNest.BusinessLogic;

namespace RavenNest.UnitTests
{

    [TestClass]
    public class DataMapperTests
    {
        [TestMethod]
        public void TestMappingNullableTypes()
        {
            var item = new DataModels.MarketItem();
            item.Flags = 10;
            var result = DataMapper.Map<RavenNest.Models.MarketItem, RavenNest.DataModels.MarketItem>(item);
            Assert.AreEqual(item.Flags.Value, result.Flags);
        }
    }

    [TestClass]
    public class ExpMultiplierTests
    {
        [TestMethod]
        public void TestMultiplier_ExpectCorrectAmount()
        {
            var player = new PlayerController();

            player.MultiplierFromHutPercent = 400;
            player.IsSubscriber = true;
            var multiWithSub = player.GetExperienceMultiplier();
            Assert.AreEqual(5 + 4 + 100, multiWithSub);

            player.IsSubscriber = false;
            var multiWithoutSub = player.GetExperienceMultiplier();
            Assert.AreEqual(4 + 100, multiWithoutSub);

            player.MultiplierFromHutPercent = 0;
            var multiWithoutSubAndHut = player.GetExperienceMultiplier();
            Assert.AreEqual(100, multiWithoutSubAndHut);

            player.MultiplierFromHutPercent = 0;
            player.BoostMultiplier = 0;
            var multiWithoutSubAndHut100 = player.GetExperienceMultiplier();
            Assert.AreEqual(1, multiWithoutSubAndHut100);
        }

        [TestMethod]
        public void TestMultiplier_ExpectCorrectAmount_Rested()
        {
            var player = new PlayerController();
            player.RestedTime = 1;

            player.MultiplierFromHutPercent = 400;
            player.IsSubscriber = true;
            var multiWithSub = player.GetExperienceMultiplier();
            Assert.AreEqual((5 + 4 + 100) * 2, multiWithSub);

            player.IsSubscriber = false;
            var multiWithoutSub = player.GetExperienceMultiplier();
            Assert.AreEqual((4 + 100) * 2, multiWithoutSub);

            player.MultiplierFromHutPercent = 0;
            var multiWithoutSubAndHut = player.GetExperienceMultiplier();
            Assert.AreEqual(100 * 2, multiWithoutSubAndHut);

            player.MultiplierFromHutPercent = 0;
            player.BoostMultiplier = 0;
            var multiWithoutSubAndHut100 = player.GetExperienceMultiplier();
            Assert.AreEqual(1 * 2, multiWithoutSubAndHut100);
        }

        private class PlayerController
        {
            public int StreamerSubscriberTier { get; set; } = 3;
            public bool IsSubscriber { get; set; }
            public float MultiplierFromHutPercent { get; set; } = 400;
            public double BoostMultiplier { get; set; } = 100;
            public float RestedExpBoost { get; set; } = 2;
            public float RestedTime { get; set; } = 0;
            public double GetTierExpMultiplier()
            {
                return this.IsSubscriber ? TwitchEventManager.TierExpMultis[StreamerSubscriberTier] : 0;
            }

            // From Village Manager

            private float GetExpBonusBySkill()
            {
                return MultiplierFromHutPercent / 100f;
            }

            public double GetExperienceMultiplier()
            {
                var tierSub = GetTierExpMultiplier();
                var multi = tierSub + GetExpBonusBySkill();
                multi += BoostMultiplier;
                multi = System.Math.Max(1, multi);

                if (RestedTime > 0 && RestedExpBoost > 1)
                    multi *= RestedExpBoost;

                return multi * GameMath.ExpScale;
            }

            public double GetCombatExperience()
            {
                return 10 * GetExperienceMultiplier();
            }

        }

        public class TwitchEventManager
        {
            public static readonly float[] TierExpMultis = new float[10]
            {
                0f, 2f, 3f, 5f, 5f, 5f, 5f, 5f, 5f, 5f
            };
        }

        private class GameMath
        {
            public const double ExpScale = 1d;
        }
    }
}

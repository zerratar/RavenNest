using Microsoft.VisualStudio.TestTools.UnitTesting;
using RavenNest.BusinessLogic;
using System;

namespace RavenNest.UnitTests
{

    [TestClass]
    public class ExtendedSkillsTest
    {
        [TestMethod]
        public void CalculateExpForNextLevelNew()
        {
            const int MaxLevel = 7109;
            double[] expLevel = new double[MaxLevel];
            for (var levelIndex = 0; levelIndex < MaxLevel; levelIndex++)
            {
                var level = levelIndex + 1M;
                var expForLevel = Math.Floor(300D * Math.Pow(2D, (double)(level / 7M)));
                expLevel[levelIndex] = Math.Round(expForLevel / 4d, 0, MidpointRounding.ToEven);
            }
        }

        [TestMethod]
        public void GetExpForLev()
        {
            var exp = GameMath.OLD_LevelToExperience(126);
        }

        [TestMethod]
        public void TestStrangeProcent()
        {
            decimal exp = 995303420;
            int level = GameMath.OLD_ExperienceToLevel(exp);
            decimal thisLevel = GameMath.OLD_LevelToExperience(level);
            decimal nextLevel = GameMath.OLD_LevelToExperience(level + 1);
            decimal deltaExp = exp - thisLevel;
            decimal deltaNextLevel = nextLevel - thisLevel;
            float procent = (float)(deltaExp / deltaNextLevel);
        }

        [TestMethod]
        public void FormatValue()
        {
            var
                str = FormatValue(10_000_000);
            str = FormatValue(1000);
            str = FormatValue(10_000);
            str = FormatValue(100);
            str = FormatValue(100_000);
        }

        private static string FormatValue(long num)
        {
            var str = num.ToString();
            if (str.Length <= 3) return str;
            for (var i = str.Length - 3; i >= 0; i -= 3)
                str = str.Insert(i, " ");
            return str;
        }
    }
}

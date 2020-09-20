using Microsoft.VisualStudio.TestTools.UnitTesting;
using RavenNest.BusinessLogic;

namespace RavenNest.UnitTests
{
    [TestClass]
    public class ExtendedSkillsTest
    {

        [TestMethod]
        public void GetExpForLev()
        {
            var exp = GameMath.LevelToExperience(126);
        }

        [TestMethod]
        public void TestStrangeProcent()
        {
            decimal exp = 995303420;
            int level = GameMath.ExperienceToLevel(exp);
            decimal thisLevel = GameMath.LevelToExperience(level);
            decimal nextLevel = GameMath.LevelToExperience(level + 1);
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

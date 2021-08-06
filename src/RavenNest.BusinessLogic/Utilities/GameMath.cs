using System;

namespace RavenNest.BusinessLogic
{
    public static class GameMath
    {
        public const int MaxLevel = 999;

        [Obsolete]
        private const int OLD_MaxLevel = 170;

        [Obsolete]
        private static decimal[] OLD_TotalExperienceArray = new decimal[OLD_MaxLevel];

        private static double[] ExperienceArray = new double[MaxLevel];

        static GameMath()
        {
            var l = 0L;
            for (var i1 = 0; i1 < OLD_MaxLevel; i1++)
            {
                var j1 = i1 + 1M;
                var l1 = (long)(j1 + (decimal)(300D * Math.Pow(2D, (double)(j1 / 7M))));
                l += l1;
                OLD_TotalExperienceArray[i1] = (decimal)((l & 0xffffffffc) / 4d);
            }

            for (var levelIndex = 0; levelIndex < MaxLevel; levelIndex++)
            {
                var level = levelIndex + 1M;
                var expForLevel = Math.Floor(300D * Math.Pow(2D, (double)(level / 7M)));
                ExperienceArray[levelIndex] = Math.Round(expForLevel / 4d, 0, MidpointRounding.ToEven);
            }
        }

        public static int MaxHit(
            int strength, int weaponPower,
            bool burst, bool superhuman, bool ultimate, int bonus)
        {
            var prayer = AddPrayers(burst, superhuman, ultimate);
            var newStrength = strength * prayer + bonus;

            var w1 = weaponPower * 0.00175D;
            var w2 = w1 + 0.1d;
            var w3 = newStrength * w2 + 1.05D;
            return (int)(w3 * 0.95d);
        }

        public static double AddPrayers(bool first, bool second, bool third)
        {
            if (third) return 1.15d;
            if (second) return 1.1d;
            if (first) return 1.05d;
            return 1.0d;
        }

        public static double ExperienceForLevel(int level)
        {
            var value = 0d;

            if (ExperienceArray.Length <= (level - 2) || level > MaxLevel)
            {
                value = ExperienceArray[ExperienceArray.Length - 1];
            }
            else if (level - 2 <= 0)
            {
                value = ExperienceArray[0];
            }
            else if (level > 2)
            {
                value = ExperienceArray[level - 2];
            }

            if (value < 0)
            {
                value = 0;
            }

            return value;
        }

        [Obsolete]
        public static int OLD_ExperienceToLevel(decimal exp)
        {
            for (int level = 0; level < OLD_MaxLevel - 1; level++)
            {
                if (exp >= OLD_TotalExperienceArray[level])
                    continue;
                return (level + 1);
            }
            return OLD_MaxLevel;
        }

        [Obsolete]
        public static decimal OLD_LevelToExperience(int level)
        {
            try
            {
                return level - 2 < 0 ? 0 : OLD_TotalExperienceArray[level - 2];
            }
            catch (Exception exc)
            {
                // not good, someone been naughty.
                return OLD_TotalExperienceArray[OLD_TotalExperienceArray.Length - 1];
            }
        }

        public static double GetFishingExperience(int level)
        {
            if (level < 15) return 25;
            if (level < 30) return 37.5;
            if (level < 45) return 100;
            if (level < 60) return 175;
            if (level < 75) return 250;

            return 10;
        }
        public static double GetFarmingExperience(int level)
        {
            if (level < 15) return 25;
            if (level < 30) return 37.5;
            if (level < 45) return 100;
            if (level < 60) return 175;
            if (level < 75) return 250;

            return 10;
        }
        public static double GetWoodcuttingExperience(int level)
        {
            /*
                Item   | Lv Req | Exp | Fatigue
                Logs	| 1	     | 25  | 0.533%
                Oak logs	15	37.5	0.8%
                Willow logs	30	62.5	1.333%
                Maple logs	45	100	2.133%
                Yew logs	60	175	3.733%
                Magic logs	75	250	5.333%
            */

            if (level < 15) return 25;
            if (level < 30) return 37.5;
            if (level < 45) return 100;
            if (level < 60) return 175;
            if (level < 75) return 250;

            return 25;
        }



        public static double GetEnchantingExperience(int skillLevel, int attributeCount, int itemLevel)
        {
            var rawExp = (attributeCount * (((1d + (skillLevel / 100d) + (skillLevel / 75d)) * (double)Math.Pow(2, (double)(skillLevel / 20d)) / 20d) + 1d)) + (attributeCount * 8.3d);
            var attrExp = (itemLevel * (attributeCount * ((double)itemLevel / MaxLevel))) * 10.0;
            var expMulti = Math.Pow(2.0, skillLevel * 0.05);
            return (rawExp + attrExp) * expMulti;
        }
    }
}

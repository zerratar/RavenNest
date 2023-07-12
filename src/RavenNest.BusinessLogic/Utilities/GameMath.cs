using System;

namespace RavenNest.BusinessLogic
{
    public static class GameMath
    {
        public const int MaxLevel = 999;
        public const int MaxVillageLevel = 300;

        public readonly static double[] ExperienceArray = new double[MaxLevel];

        [Obsolete]
        private static readonly double[] OldExperienceArray = new double[MaxLevel];

        static GameMath()
        {
            var expForLevel = 100d;
            for (var levelIndex = 0; levelIndex < MaxLevel; levelIndex++)
            {
                var level = levelIndex + 1;

                // new
                var tenth = Math.Truncate(level / 10d) + 1;
                var incrementor = tenth * 100d + Math.Pow(tenth, 3d);
                expForLevel += Math.Truncate(incrementor);
                ExperienceArray[levelIndex] = expForLevel;
            }

            // Old formula
            for (var levelIndex = 0; levelIndex < MaxLevel; levelIndex++)
            {
                var level = levelIndex + 1M;
                expForLevel = Math.Floor(300D * Math.Pow(2D, (double)(level / 7M)));
                OldExperienceArray[levelIndex] = Math.Round(expForLevel / 4d, 0, MidpointRounding.ToEven);
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
            if (level - 2 >= ExperienceArray.Length)
            {
                return ExperienceArray[ExperienceArray.Length - 1];
            }

            return (level - 2 < 0 ? 0 : ExperienceArray[level - 2]);
        }


        [Obsolete]
        public static double OldExperienceForLevel(int level)
        {
            var value = 0d;

            if (OldExperienceArray.Length <= (level - 2) || level > MaxLevel)
            {
                value = OldExperienceArray[OldExperienceArray.Length - 1];
            }
            else if (level - 2 <= 0)
            {
                value = OldExperienceArray[0];
            }
            else if (level > 2)
            {
                value = OldExperienceArray[level - 2];
            }

            if (value < 0)
            {
                value = 0;
            }

            return value;
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

        internal static double GetVillageExperience(int level, double exp)
        {
            var oldNextLevelReq = OldExperienceForLevel(level + 1);
            var ratio = exp / oldNextLevelReq;
            return ExperienceForLevel(level + 1) * ratio;
        }

        public static double GetClanExperience(int level, double exp)
        {
            var oldNextLevelReq = OldExperienceForLevel(level + 1);
            var ratio = exp / oldNextLevelReq;
            return ExperienceForLevel(level + 1) * ratio;
        }

        public static double GetEnchantingExperience(int skillLevel, int attributeCount, int itemLevel)
        {
            var rawExp = (attributeCount * (((1d + (skillLevel / 100d) + (skillLevel / 75d)) * (double)Math.Pow(2, (double)(skillLevel / 20d)) / 20d) + 1d)) + (attributeCount * 8.3d);
            var attrExp = (itemLevel * (attributeCount * ((double)itemLevel / MaxLevel))) * 10.0;
            var expMulti = Math.Pow(2.0, skillLevel * 0.05);
            var expToGain = (rawExp + attrExp) * expMulti * 0.4;

            // scale the old exp gain to new one.
            var oldNextLevelReq = OldExperienceForLevel(skillLevel + 1);
            var nextLevelReq = ExperienceForLevel(skillLevel + 1);
            var percentGain = expToGain / oldNextLevelReq;

            expToGain = nextLevelReq * percentGain;


            return expToGain;
        }

    }
}

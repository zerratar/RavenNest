using System;

namespace RavenNest.BusinessLogic
{
    public static class GameMath
    {
        public const int MaxLevel = 170;

        //public static readonly double[] ExperienceArray =
        //{
        //        83, 174, 276, 388, 512, 650, 801, 969, 1154, 1358, 1584, 1833, 2107, 2411, 2746, 3115, 3523, 3973,
        //        4470, 5018, 5624, 6291, 7028, 7842, 8740, 9730, 10824, 12031, 13363, 14833, 16456, 18247, 20224, 22406,
        //        24815, 27473, 30408, 33648, 37224, 41171, 45529, 50339, 55649, 61512, 67983, 75127, 83014, 91721, 101333,
        //        111945, 123660, 136594, 150872, 166636, 184040, 203254, 224466, 247886, 273742, 302288, 333804, 368599,
        //        407015, 449428, 496254, 547953, 605032, 668051, 737627, 814445, 899257, 992895, 1096278, 1210421, 1336443,
        //        1475581, 1629200, 1798808, 1986068, 2192818, 2421087, 2673114, 2951373, 3258594, 3597792, 3972294, 4385776,
        //        4842295, 5346332, 5902831, 6517253, 7195629, 7944614, 8771558, 9684577, 10692629, 11805606, 13034431, 14391160
        //    };

        private static decimal[] ExperienceArray = new decimal[MaxLevel];

        static GameMath()
        {
            //long l = 0;
            //for (var i1 = 0; i1 < MaxLevel; i1++)
            //{
            //    var j1 = i1 + 1;
            //    var l1 = (int)(j1 + 300D * Math.Pow(2D, j1 / 7D));
            //    l += l1;
            //    ExperienceArray[i1] = (l & 0xffffffffc) / 4d;
            //}

            var l = 0L;
            for (var i1 = 0; i1 < MaxLevel; i1++)
            {
                var j1 = i1 + 1M;
                var l1 = (long)(j1 + (decimal)(300D * Math.Pow(2D, (double)(j1 / 7M))));
                l += l1;
                ExperienceArray[i1] = (decimal)((l & 0xffffffffc) / 4d);
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

        public static int CombatExperience(int combatLevel)
        {
            return (int)((combatLevel * 10 + 10) * 1.5D);
        }

        public static int ExperienceToLevel(decimal exp)
        {
            for (int level = 0; level < MaxLevel - 1; level++)
            {
                if (exp >= ExperienceArray[level])
                    continue;
                return (level + 1);
            }
            return MaxLevel;
        }

        public static decimal LevelToExperience(int level)
        {
            return level - 2 < 0 ? 0 : ExperienceArray[level - 2];
        }

        public static decimal GetFishingExperience(int level)
        {
            if (level < 15) return 25;
            if (level < 30) return 37.5m;
            if (level < 45) return 100;
            if (level < 60) return 175;
            if (level < 75) return 250;

            return 10;
        }
        public static decimal GetFarmingExperience(int level)
        {
            if (level < 15) return 25;
            if (level < 30) return 37.5m;
            if (level < 45) return 100;
            if (level < 60) return 175;
            if (level < 75) return 250;

            return 10;
        }
        public static decimal GetWoodcuttingExperience(int level)
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
            if (level < 30) return 37.5m;
            if (level < 45) return 100;
            if (level < 60) return 175;
            if (level < 75) return 250;

            return 25;
        }
    }
}
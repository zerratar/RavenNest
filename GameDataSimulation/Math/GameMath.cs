using System.Runtime.CompilerServices;

namespace GameDataSimulation
{
    public static class GameMath
    {
        public const int MaxLevel = 999;
        public readonly static double[] ExperienceArray = new double[MaxLevel];
        static GameMath()
        {
            for (var levelIndex = 0; levelIndex < MaxLevel; levelIndex++)
            {
                var level = levelIndex + 1M;
                var expForLevel = Math.Floor(300D * Math.Pow(2D, (double)(level / 7M)));
                ExperienceArray[levelIndex] = Math.Round(expForLevel / 4d, 0, MidpointRounding.ToEven);
            }
        }

        public static double GetFishingExperience(int level)
        {
            return (level * 0.66d) + (level * (level / 40d)) + (level * level * 0.005d) + level * 0.5d;
        }
        public static double GetFarmingExperience(int level)
        {
            /*
                Following formula will generate:
                Level   Exp
                15		24,15
                30		61,8
                45		112,95
                60		177,6
                75		255,75
                90		347,4
                110		490,6
             */
            return (level * 0.66d) + (level * (level / 40d)) + (level * level * 0.005d) + level * 0.5d;
        }

        public static double GetWoodcuttingExperience(int level)
        {
            return (level * 0.66d) + (level * (level / 40d)) + (level * level * 0.005d) + level * 0.5d;
        }


        public static double ExperienceForLevel(int level)
        {
            if (level - 2 >= ExperienceArray.Length)
            {
                return ExperienceArray[ExperienceArray.Length - 1];
            }
            return (level - 2 < 0 ? 0 : ExperienceArray[level - 2]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Lerp(double v1, double v2, double t)
        {
            return v1 + (v2 - v1) * t;
        }

        public static class Exp
        {
            /// <summary>
            /// The level where time between level has peaked at <see cref="IncrementMins"/>.
            /// </summary>
            public const double EasyLevel = 75.0;

            public const double IncrementMins = 14.0;
            public const double IncrementHours = IncrementMins / 60.0;
            public const double IncrementDays = IncrementHours / 24.0;
            public const double MaxLevelDays = IncrementDays * MaxLevel;
            public const double MultiEffectiveness = 1.375d;

            public const double MaxExpFactorFromIsland = 1d;

            /// <summary>
            /// Calculates the amount of exp that should be yielded given the current skill and level.
            /// </summary>
            /// <param name="nextLevel"></param>
            /// <param name="skill"></param>
            /// <param name="factor"></param>
            /// <param name="boost"></param>
            /// <param name="multiplierFactor"></param>
            /// <returns></returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static double CalculateExperience(int nextLevel, Skill skill, double factor = 1, double boost = 1, double multiplierFactor = 1)
            {
                var bTicksForLevel = GetTotalTicksForLevel(nextLevel, skill, boost);
                var expForNextLevel = ExperienceForLevel(nextLevel);
                var maxExpGain = expForNextLevel / bTicksForLevel;
                var minExpGainPercent = GetMinExpGainPercent(nextLevel, skill);
                var minExpGain = ExperienceForLevel(nextLevel) * minExpGainPercent;
                return Lerp(0, Lerp(minExpGain, maxExpGain, multiplierFactor), factor);
            }

            /// <summary>
            /// Gets the total amount of "Ticks" to level up to the given target level after applying the exp boost.
            /// </summary>
            /// <param name="level"></param>
            /// <param name="skill"></param>
            /// <param name="multiplier"></param>
            /// <param name="playersInArea"></param>
            /// <returns></returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static double GetTotalTicksForLevel(int level, Skill skill, double multiplier = 1, int playersInArea = 100)
            {
                return GetTotalTicksForLevel(level, skill, playersInArea) / GetEffectiveExpMultiplier(level, multiplier);
            }

            /// <summary>
            /// Gets the total amount of "Ticks" to level up to the given target level. Without applying any exp boost.
            /// </summary>
            /// <param name="level"></param>
            /// <param name="skill"></param>
            /// <param name="playersInArea"></param>
            /// <returns></returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static double GetTotalTicksForLevel(int level, Skill skill, int playersInArea = 100)
            {
                return GetMaxMinutesForLevel(level) * GetTicksPerMinute(skill, playersInArea);
            }

            /// <summary>
            /// Gets the effective exp multiplier given the current multiplier and player level; 
            /// This is multiplied by the exp given by one "Tick"
            /// </summary>
            /// <param name="multiplier">Expected to be in full form (100 and not 1.0)</param>
            /// <returns></returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static double GetEffectiveExpMultiplier(int level, double multiplier = 1)
            {
                return Math.Max(Math.Min((((MaxLevel * MultiEffectiveness) - (level - 1)) / (MaxLevel * MultiEffectiveness)) * multiplier, multiplier), 1.0);
            }

            /// <summary>
            /// Gets the minimum exp gain in percent towards the next skill level. Is to boost up exp gains for higher levels.
            /// </summary>
            /// <param name="nextLevel"></param>
            /// <param name="skill"></param>
            /// <param name="playersInArea"></param>
            /// <returns></returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static double GetMinExpGainPercent(int nextLevel, Skill skill, int playersInArea = 100)
            {
                return 1d / (GetTicksPerMinute(skill, playersInArea) * GetMaxMinutesForLevel(nextLevel));
            }

            /// <summary>
            /// Gets the maximum possible time needed to level up from level-1 to target level.
            /// </summary>
            /// <param name="level"></param>
            /// <returns></returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static double GetMaxMinutesForLevel(int level)
            {
                if (level <= EasyLevel)
                {
                    return (level - 1) * GameMath.Lerp(IncrementMins / 4.0d, IncrementMins, level / EasyLevel);
                }

                return (level - 1) * IncrementMins;
            }

            /// <summary>
            /// Gets the expected exp ticks per minutes the target skill and players training the same thing in the area.
            /// These values are taken from real world cases and used as an estimate.
            /// </summary>
            /// <param name="skill"></param>
            /// <param name="playersInArea"></param>
            /// <returns></returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static double GetTicksPerMinute(Skill skill, int playersInArea = 100)
            {
                return GetTicksPerSeconds(skill, playersInArea) * 60;
            }

            /// <summary>
            /// Get the expected exp ticks per seconds given the target skill and players training the same thing in the area.
            /// These values are taken from real world cases and used as an estimate.
            /// </summary>
            /// <param name="skill"></param>
            /// <param name="playersInArea"></param>
            /// <returns></returns>
            public static double GetTicksPerSeconds(Skill skill, int playersInArea = 100)
            {
                switch (skill)
                {
                    case Skill.Woodcutting when playersInArea < 100: return 0.15;
                    case Skill.Woodcutting when playersInArea >= 100: return 0.33;
                    case Skill.Farming:
                    case Skill.Crafting:
                    case Skill.Cooking:
                    case Skill.Fishing:
                        return 1d / 3d;

                    case Skill.Mining:
                        return 0.5;

                    case (Skill.Health or Skill.Attack or Skill.Defense or Skill.Strength or Skill.Magic or Skill.Ranged) when playersInArea < 100:
                        return 0.75;

                    case (Skill.Health or Skill.Attack or Skill.Defense or Skill.Strength or Skill.Magic or Skill.Ranged) when playersInArea >= 100:
                        return 1.25;

                    case Skill.Healing: return 0.5d;
                    case Skill.Sailing: return 0.4d;
                    default: return 0.5;
                }
            }
        }
    }


    public enum Skill
    {
        Attack = 0,
        Defense = 1,
        Strength = 2,
        Health = 3,
        Woodcutting = 4,
        Fishing = 5,
        Mining = 6,
        Crafting = 7,
        Cooking = 8,
        Farming = 9,
        Slayer = 10,
        Magic = 11,
        Ranged = 12,
        Sailing = 13,
        Healing = 14,
    }
}

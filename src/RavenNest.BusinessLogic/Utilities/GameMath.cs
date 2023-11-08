using RavenNest.DataModels;
using RavenNest.Models;
using System;
using System.Runtime.CompilerServices;

namespace RavenNest.BusinessLogic
{
    public static class GameMath
    {
        public const int MaxLevel = 999;
        public const int MaxVillageLevel = 300;
        public const int MaximumEnchantmentCount = 10;

        public const double MaxExpGainPercentageForEnchanting = 1.5;
        public const double MinExpGainPercentageForEnchanting = 0.001;

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

        public static double CalculateSuccessRate(int level, int requiredLevel, double minSuccessRate, double maxSuccessRate, double alpha = 0.01)
        {
            if (level < requiredLevel) return 0;
            var calculatedSuccessRate = minSuccessRate + (0.95 * (1 - Math.Exp(-alpha * (level - requiredLevel))));
            return Math.Min(maxSuccessRate, calculatedSuccessRate);
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

        public static double GetVillageExperience(int level, int playerCount, TimeSpan elapsedTime)
        {
            // there is a 1% increase of exp per player up to 300 players. (3x max)
            // Base Speed is 1/10th of a skill like cooking,crafting,mining,fishing,farming that has a constant exp gain.
            // every level above 75 (EasyLevel) will add 140 minutes per level, every level below 75 will add a lerped amount from 17,5 minutes to 140 with the amount of (current Level-2) / 75.
            // multiplier effects time, not the actual exp amount. so 3x is 3 times less time to level up compared to the base time.
            // this means, 17min, 30s / 4 (max gain where 1+3) = 4 min, 22s, more players are not linear in exp gain but they most certainly help still.

            playerCount++;

            var playerFactor = 1 + (playerCount / 2000d);
            var factor = playerFactor * elapsedTime.TotalSeconds * 0.075;

            var nextlevel = level + 1;
            var bTicksForLevel = Exp.GetMaxMinutesForLevel(nextlevel) * 60; // multiply by 60 to get seconds, since we have 1 tick per second
            var expForNextLevel = ExperienceForLevel(nextlevel);
            var expGain = expForNextLevel / bTicksForLevel;
            var value = Lerp(0, expGain, factor);
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                return 0;
            }
            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double GetClanExperience(int level, TimeSpan elapsedTime)
        {
            return Exp.CalculateExperience(level + 1, 1) * elapsedTime.TotalSeconds * 0.025d;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetItemLevel(DataModels.Item i)
        {
            var lv = (i.RequiredAttackLevel + i.RequiredDefenseLevel + i.RequiredMagicLevel + i.RequiredRangedLevel + i.RequiredSlayerLevel);
            if (lv > i.Level) return lv;
            return i.Level;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetItemLevel(RavenNest.Models.Item i)
        {
            var lv = (i.RequiredAttackLevel + i.RequiredDefenseLevel + i.RequiredMagicLevel + i.RequiredRangedLevel + i.RequiredSlayerLevel);
            if (lv > i.Level) return lv;
            return i.Level;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetMaxEnchantingAttributeCount(DataModels.Item i)
        {
            var itemLvReq = GetItemLevel(i);
            return GetMaxEnchantingAttributeCount(itemLvReq);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetMaxEnchantingAttributeCount(int itemLevel)
        {
            return Math.Max(1, (int)Math.Floor(Math.Floor(itemLevel / 10f) / 5));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetMaxEnchantmentCountBySkill(int skillLevel)
        {
            return (int)Math.Floor(Math.Max(MaximumEnchantmentCount, Math.Floor(skillLevel / 3f)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double GetEnchantingExperience(int skillLevel, DataModels.Item i)
        {
            var itemLvReq = GetItemLevel(i);
            return GetEnchantingExperience(skillLevel, itemLvReq);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double GetEnchantingExperience(int skillLevel, int itemLevel)
        {
            var attributeCount = GetMaxEnchantingAttributeCount(itemLevel);
            var maxEnchantments = GetMaxEnchantmentCountBySkill(skillLevel);
            var targetAttributeCount = Math.Max(0, Math.Min(maxEnchantments, attributeCount));
            return GetEnchantingExperience(skillLevel, targetAttributeCount, itemLevel);
        }

        public static double GetEnchantingExperience(int skillLevel, int attributeCount, int itemLevel)
        {
            var exp = itemLevel * 100d;
            exp += attributeCount * 25d;
            var lv = (double)skillLevel / MaxLevel;
            exp += (exp * lv * 0.25d);
            var expForLevel = ExperienceForLevel(skillLevel + 1);

            return Math.Truncate(
                Math.Max(expForLevel * MinExpGainPercentageForEnchanting, Math.Min(exp, expForLevel * MaxExpGainPercentageForEnchanting))
            );
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Lerp(double v1, double v2, double t)
        {
            return v1 + (v2 - v1) * t;
        }

        /// <summary>
        ///     How much will you have to pay to buy an item from the vendor?
        /// </summary>
        /// <param name="i"></param>
        /// <param name="inStock"></param>
        /// <returns></returns>
        public static long CalculateVendorBuyPrice(DataModels.Item i, long inStock)
        {
            var minPrice = Math.Max(i.ShopSellPrice, i.ShopBuyPrice);
            return CalculateVendorBuyPrice(minPrice, inStock);
        }

        /// <summary>
        ///     How much will you have to pay to buy an item from the vendor?
        /// </summary>
        /// <param name="i"></param>
        /// <param name="inStock"></param>
        /// <returns></returns>
        public static long CalculateVendorBuyPrice(RavenNest.Models.Item i, long inStock)
        {
            var minPrice = Math.Max(i.ShopSellPrice, i.ShopBuyPrice);
            return CalculateVendorBuyPrice(minPrice, inStock);
        }

        /// <summary>
        ///     How much will you have to pay to buy an item from the vendor?
        /// </summary>
        /// <param name="i"></param>
        /// <param name="inStock"></param>
        /// <returns></returns>
        public static long CalculateVendorBuyPrice(long minPrice, long inStock = 0)
        {
            var price = (double)Math.Truncate(minPrice * 1.25d);

            // full buy price until there are more than 10 items in stock
            if (inStock - 10 <= 0)
            {
                return (long)price;
            }

            // reduce 1% price every 10 items in stock
            // until we reach 75% off

            var min = Math.Max(1, (long)Math.Truncate(price * 0.25d));
            var reductionCount = Math.Truncate(inStock / 10.0);
            price -= reductionCount * price * 0.01d;
            if ((long)price <= min) return min;
            return (long)price;
        }

        /// <summary>
        ///     How much will you get for selling an item to the vendor?
        /// </summary>
        /// <param name="i"></param>
        /// <param name="inStock"></param>
        /// <returns></returns>
        public static long CalculateVendorSellPrice(DataModels.Item i, long inStock)
        {
            var minPrice = i.ShopSellPrice;
            // if there are more than 5 items in stock, we will start selling for less.
            if (inStock - 5 <= 0)
            {
                return minPrice;
            }

            inStock = inStock - 5;

            // reduce 5% price every 5 items in stock
            var reductionCount = Math.Truncate(inStock / 5.0d);
            double price = minPrice;
            for (var j = 0; j < reductionCount; ++j)
            {
                if (price <= 1) return 1;
                price -= (minPrice * 0.05d);
            }

            return Math.Max(1, (long)price);
        }

        public static class Exp
        {
            // Old Values
            public static double EasyLevel = 70;
            public static double IncrementMins = 14;
            public static double EasyLevelIncrementDivider = 8;
            public static double GlobalMultiplierFactor = 1.0;

            // New Values, change to these after everyone updated to latest version.
            //public static double EasyLevel = 700;
            //public static double IncrementMins = 5;
            //public static double EasyLevelIncrementDivider = 12.5;
            //public static double GlobalMultiplierFactor = 0.05;

            public static double IncrementHours = IncrementMins / 60.0;
            public static double IncrementDays = IncrementHours / 24.0;
            public static double MaxLevelDays = IncrementDays * MaxLevel;
            public static double MultiEffectiveness = 1.375d;
            public static double MaxExpFactorFromIsland = 1d;
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
                var minExpGain = expForNextLevel * minExpGainPercent;
                return Lerp(0, Lerp(minExpGain, maxExpGain, multiplierFactor), factor);
            }

            public static double CalculateExperience(int nextLevel, double ticksPerSeconds, double factor = 1, double boost = 1, double multiplierFactor = 1)
            {
                var bTicksForLevel = GetTotalTicksForLevel(nextLevel, ticksPerSeconds);
                var expForNextLevel = ExperienceForLevel(nextLevel);
                var maxExpGain = expForNextLevel / bTicksForLevel;
                var minExpGainPercent = GetMinExpGainPercent(nextLevel, ticksPerSeconds);
                var minExpGain = expForNextLevel * minExpGainPercent;
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
            /// Gets the total amount of "Ticks" to level up to the given target level. Without applying any exp boost.
            /// </summary>
            /// <param name="level"></param>
            /// <returns></returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static double GetTotalTicksForLevel(int level, double ticksPerSeconds)
            {
                return GetMaxMinutesForLevel(level) * (ticksPerSeconds * 60);
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
                return Math.Max(Math.Min((((MaxLevel * MultiEffectiveness) - (level - 1)) / (MaxLevel * MultiEffectiveness)) * multiplier, multiplier) * GlobalMultiplierFactor, 1.0);
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

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static double GetMinExpGainPercent(int nextLevel, double ticksPerSeceond)
            {
                return 1d / ((ticksPerSeceond * 60) * GetMaxMinutesForLevel(nextLevel));
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
                    var a = Math.Max(0, (level - 2) / EasyLevel);
                    var increment = Lerp(IncrementMins / EasyLevelIncrementDivider, IncrementMins, a);
                    return (level - 1) * increment;
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
                    case Skill.Alchemy:
                    case Skill.Gathering:
                        return 1d / 3d;

                    case Skill.Mining:
                        return 0.5;

                    case (Skill.Health or Skill.Attack or Skill.Defense or Skill.Strength or Skill.Magic or Skill.Ranged) when playersInArea < 10:
                        return 0.25;

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
            Gathering = 15,
            Alchemy = 16,
        }
    }
}

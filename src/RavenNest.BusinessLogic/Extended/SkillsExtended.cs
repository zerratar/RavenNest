using System;
using System.Runtime.CompilerServices;

namespace RavenNest.BusinessLogic.Extended
{
    public class SkillsExtended
    {
        public Guid Id { get; set; }
        public decimal Attack { get; set; }
        public decimal Defense { get; set; }
        public decimal Strength { get; set; }
        public decimal Health { get; set; }
        public decimal Magic { get; set; }
        public decimal Ranged { get; set; }
        public decimal Woodcutting { get; set; }
        public decimal Fishing { get; set; }
        public decimal Mining { get; set; }
        public decimal Crafting { get; set; }
        public decimal Cooking { get; set; }
        public decimal Farming { get; set; }
        public decimal Slayer { get; set; }
        public decimal Sailing { get; set; }
        public int Revision { get; set; }

        public int AttackLevel { get; set; }
        public int DefenseLevel { get; set; }
        public int StrengthLevel { get; set; }
        public int HealthLevel { get; set; }
        public int MagicLevel { get; set; }
        public int RangedLevel { get; set; }
        public int WoodcuttingLevel { get; set; }
        public int FishingLevel { get; set; }
        public int MiningLevel { get; set; }
        public int CraftingLevel { get; set; }
        public int CookingLevel { get; set; }
        public int FarmingLevel { get; set; }
        public int SlayerLevel { get; set; }
        public int SailingLevel { get; set; }

        public float AttackProcent => GetPercentForNextLevel(AttackLevel, Attack);
        public float DefenseProcent => GetPercentForNextLevel(DefenseLevel, Defense);
        public float StrengthProcent => GetPercentForNextLevel(StrengthLevel, Strength);
        public float HealthProcent => GetPercentForNextLevel(HealthLevel, Health);
        public float MagicProcent => GetPercentForNextLevel(MagicLevel, Magic);
        public float RangedProcent => GetPercentForNextLevel(RangedLevel, Ranged);
        public float WoodcuttingProcent => GetPercentForNextLevel(WoodcuttingLevel, Woodcutting);
        public float FishingProcent => GetPercentForNextLevel(FishingLevel, Fishing);
        public float MiningProcent => GetPercentForNextLevel(MiningLevel, Mining);
        public float CraftingProcent => GetPercentForNextLevel(CraftingLevel, Crafting);
        public float CookingProcent => GetPercentForNextLevel(CookingLevel, Cooking);
        public float FarmingProcent => GetPercentForNextLevel(FarmingLevel, Farming);
        public float SlayerProcent => GetPercentForNextLevel(SlayerLevel, Slayer);
        public float SailingProcent => GetPercentForNextLevel(SailingLevel, Sailing);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float GetPercentForNextLevel(int level, decimal exp)
        {
            var nextLevel = GameMath.ExperienceForLevel(level + 1);
            var thisLevel = exp;

            //var level = GameMath.OLD_ExperienceToLevel(exp);
            //var thisLevel = GameMath.OLD_LevelToExperience(level);
            return (float)(thisLevel / nextLevel);
        }
    }
}

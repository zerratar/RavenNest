using RavenNest.Models;
using System;
using System.Runtime.CompilerServices;

namespace RavenNest.BusinessLogic.Extended
{
    public class SkillsExtended : Skills
    {
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

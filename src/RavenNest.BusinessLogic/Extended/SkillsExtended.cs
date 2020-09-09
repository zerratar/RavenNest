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

        public int AttackLevel => GameMath.ExperienceToLevel(Attack);
        public int DefenseLevel => GameMath.ExperienceToLevel(Defense);
        public int StrengthLevel => GameMath.ExperienceToLevel(Strength);
        public int HealthLevel => GameMath.ExperienceToLevel(Health);
        public int MagicLevel => GameMath.ExperienceToLevel(Magic);
        public int RangedLevel => GameMath.ExperienceToLevel(Ranged);
        public int WoodcuttingLevel => GameMath.ExperienceToLevel(Woodcutting);
        public int FishingLevel => GameMath.ExperienceToLevel(Fishing);
        public int MiningLevel => GameMath.ExperienceToLevel(Mining);
        public int CraftingLevel => GameMath.ExperienceToLevel(Crafting);
        public int CookingLevel => GameMath.ExperienceToLevel(Cooking);
        public int FarmingLevel => GameMath.ExperienceToLevel(Farming);
        public int SlayerLevel => GameMath.ExperienceToLevel(Slayer);
        public int SailingLevel => GameMath.ExperienceToLevel(Sailing);

        public float AttackProcent => GetPercentForNextLevel(Attack);
        public float DefenseProcent => GetPercentForNextLevel(Defense);
        public float StrengthProcent => GetPercentForNextLevel(Strength);
        public float HealthProcent => GetPercentForNextLevel(Health);
        public float MagicProcent => GetPercentForNextLevel(Magic);
        public float RangedProcent => GetPercentForNextLevel(Ranged);
        public float WoodcuttingProcent => GetPercentForNextLevel(Woodcutting);
        public float FishingProcent => GetPercentForNextLevel(Fishing);
        public float MiningProcent => GetPercentForNextLevel(Mining);
        public float CraftingProcent => GetPercentForNextLevel(Crafting);
        public float CookingProcent => GetPercentForNextLevel(Cooking);
        public float FarmingProcent => GetPercentForNextLevel(Farming);
        public float SlayerProcent => GetPercentForNextLevel(Slayer);
        public float SailingProcent => GetPercentForNextLevel(Sailing);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float GetPercentForNextLevel(decimal exp)
        {
            var level = GameMath.ExperienceToLevel(exp);
            var thisLevel = GameMath.LevelToExperience(level);
            return (float)((exp - thisLevel) / (GameMath.LevelToExperience(level + 1) - thisLevel));
        }
    }
}

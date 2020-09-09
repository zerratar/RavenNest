using System;

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

        public decimal AttackProcent => (Attack / GameMath.LevelToExperience(AttackLevel + 1));
        public decimal DefenseProcent => (Defense / GameMath.LevelToExperience(DefenseLevel + 1));
        public decimal StrengthProcent => (Strength / GameMath.LevelToExperience(StrengthLevel + 1));
        public decimal HealthProcent => (Health / GameMath.LevelToExperience(HealthLevel + 1));
        public decimal MagicProcent => (Magic / GameMath.LevelToExperience(MagicLevel + 1));
        public decimal RangedProcent => (Ranged / GameMath.LevelToExperience(RangedLevel + 1));
        public decimal WoodcuttingProcent => (Woodcutting / GameMath.LevelToExperience(WoodcuttingLevel + 1));
        public decimal FishingProcent => (Fishing / GameMath.LevelToExperience(FishingLevel + 1));
        public decimal MiningProcent => (Mining / GameMath.LevelToExperience(MiningLevel + 1));
        public decimal CraftingProcent => (Crafting / GameMath.LevelToExperience(CraftingLevel + 1));
        public decimal CookingProcent => (Cooking / GameMath.LevelToExperience(CookingLevel + 1));
        public decimal FarmingProcent => (Farming / GameMath.LevelToExperience(FarmingLevel + 1));
        public decimal SlayerProcent => (Slayer / GameMath.LevelToExperience(SlayerLevel + 1));
        public decimal SailingProcent => (Sailing / GameMath.LevelToExperience(SailingLevel + 1));
    }
}

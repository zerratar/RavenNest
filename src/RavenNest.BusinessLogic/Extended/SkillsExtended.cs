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

        public double AttackProcent => (double)(Attack / GameMath.ExperienceToLevel(AttackLevel + 1));
        public double DefenseProcent => (double)(Attack / GameMath.ExperienceToLevel(DefenseLevel + 1));
        public double StrengthProcent => (double)(Attack / GameMath.ExperienceToLevel(StrengthLevel + 1));
        public double HealthProcent => (double)(Attack / GameMath.ExperienceToLevel(HealthLevel + 1));
        public double MagicProcent => (double)(Attack / GameMath.ExperienceToLevel(MagicLevel + 1));
        public double RangedProcent => (double)(Attack / GameMath.ExperienceToLevel(RangedLevel + 1));
        public double WoodcuttingProcent => (double)(Attack / GameMath.ExperienceToLevel(WoodcuttingLevel + 1));
        public double FishingProcent => (double)(Attack / GameMath.ExperienceToLevel(FishingLevel + 1));
        public double MiningProcent => (double)(Attack / GameMath.ExperienceToLevel(MiningLevel + 1));
        public double CraftingProcent => (double)(Attack / GameMath.ExperienceToLevel(CraftingLevel + 1));
        public double CookingProcent => (double)(Attack / GameMath.ExperienceToLevel(CookingLevel + 1));
        public double FarmingProcent => (double)(Attack / GameMath.ExperienceToLevel(FarmingLevel + 1));
        public double SlayerProcent => (double)(Attack / GameMath.ExperienceToLevel(SlayerLevel + 1));
        public double SailingProcent => (double)(Attack / GameMath.ExperienceToLevel(SailingLevel + 1));
    }
}

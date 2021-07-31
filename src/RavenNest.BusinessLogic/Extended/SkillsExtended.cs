using RavenNest.Models;
using System;
using System.Collections.Generic;
using System.Linq;
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
        public float HealingProcent => GetPercentForNextLevel(HealingLevel, Healing);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float GetPercentForNextLevel(int level, double exp)
        {
            var nextLevel = GameMath.ExperienceForLevel(level + 1);
            var thisLevel = exp;

            //var level = GameMath.OLD_ExperienceToLevel(exp);
            //var thisLevel = GameMath.OLD_LevelToExperience(level);
            return (float)(thisLevel / nextLevel);
        }

        public IReadOnlyList<PlayerSkill> AsList()
        {
            var props = typeof(SkillsExtended)
                .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                .ToDictionary(x => x.Name, x => x);
            var skills = new List<PlayerSkill>();
            var names = props.Values.Where(x => x.Name.EndsWith("Level")).Select(x => x.Name.Replace("Level", ""));
            foreach (var name in names)
            {
                var n = name;
                var experience = (double)props[n].GetValue(this);
                var level = (int)props[n + "Level"].GetValue(this);
                var percent = (float)props[n + "Procent"].GetValue(this);
                skills.Add(new PlayerSkill
                {
                    Name = n,
                    Experience = experience,
                    Level = level,
                    Percent = percent
                });
            }

            return skills;
        }
    }

    public class PlayerSkill
    {
        public string Name { get; set; }
        public int Level { get; set; }
        public double Experience { get; set; }
        public float Percent { get; set; }
    }
}

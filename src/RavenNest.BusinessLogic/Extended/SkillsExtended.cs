using RavenNest.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
        public static float GetPercentForNextLevel(int level, double exp)
        {
            var nextLevel = GameMath.ExperienceForLevel(level + 1);
            var thisLevel = exp;

            //var level = GameMath.OLD_ExperienceToLevel(exp);
            //var thisLevel = GameMath.OLD_LevelToExperience(level);
            return (float)(thisLevel / nextLevel);
        }

        public IReadOnlyList<PlayerSkill> AsList()
        {
            var props = GetProperties();
            var skills = new List<PlayerSkill>();
            var names = props.Values.Where(x => x.Property.Name.EndsWith("Level")).OrderBy(x => x.SortIndex).Select(x => x.Property.Name.Replace("Level", ""));
            foreach (var name in names)
            {
                var n = name;
                var experience = (double)props[n].Property.GetValue(this);
                var level = (int)props[n + "Level"].Property.GetValue(this);
                var percent = (float)props[n + "Procent"].Property.GetValue(this);
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

        public IReadOnlyDictionary<string, PlayerSkill> AsDirectionary()
        {
            var props = GetProperties();
            var skills = new Dictionary<string, PlayerSkill>();
            var names = props.Values.Where(x => x.Property.Name.EndsWith("Level")).OrderBy(x => x.SortIndex).Select(x => x.Property.Name.Replace("Level", ""));
            foreach (var name in names)
            {
                var n = name;
                var experience = (double)props[n].Property.GetValue(this);
                var level = (int)props[n + "Level"].Property.GetValue(this);
                var percent = (float)props[n + "Procent"].Property.GetValue(this);
                skills.Add(n, new PlayerSkill
                {
                    Name = n,
                    Experience = experience,
                    Level = level,
                    Percent = percent
                });
            }

            return skills;
        }

        private static ConcurrentDictionary<string, SkillPropertyInfo> propertyCache = new ConcurrentDictionary<string, SkillPropertyInfo>();
        private static ConcurrentDictionary<string, SkillPropertyInfo> GetProperties()
        {
            if (propertyCache == null || propertyCache.Count == 0)
            {
                var properties = typeof(SkillsExtended).GetProperties(BindingFlags.Public | BindingFlags.Instance);

                propertyCache = new ConcurrentDictionary<string, SkillPropertyInfo>(properties.ToDictionary(x => x.Name, x =>
                    new SkillPropertyInfo { Property = x, SortIndex = Array.IndexOf(properties, x) }
                ));
            }

            return propertyCache;
        }
    }

    public class SkillPropertyInfo
    {
        public int SortIndex;
        public PropertyInfo Property;
    }

    public class PlayerSkill
    {
        public string Name { get; set; }
        public int Level { get; set; }
        public double Experience { get; set; }
        public float Percent { get; set; }
    }
}

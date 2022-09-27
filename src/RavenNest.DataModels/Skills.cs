using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace RavenNest.DataModels
{
    public partial class Skills : Entity<Skills>
    {
        public static readonly string[] SkillNames = new string[] {
            nameof(Attack),
            nameof(Defense),
            nameof(Strength),
            nameof(Health),
            nameof(Woodcutting),
            nameof(Fishing),
            nameof(Mining),
            nameof(Crafting),
            nameof(Cooking),
            nameof(Farming),
            nameof(Slayer),
            nameof(Magic),
            nameof(Ranged),
            nameof(Sailing),
            nameof(Healing)
        };

        private static ConcurrentDictionary<string, PropertyInfo> expProperties = new ConcurrentDictionary<string, PropertyInfo>();
        private static ConcurrentDictionary<string, PropertyInfo> levelProperties = new ConcurrentDictionary<string, PropertyInfo>();
        private List<StatsUpdater> skills;

        private double attack;
        private double defense;
        private double strength;
        private double health;
        private double magic;
        private double ranged;
        private double woodcutting;
        private double fishing;
        private double mining;
        private double crafting;
        private double cooking;
        private double farming;
        private double slayer;
        private double sailing;
        private double healing;
        private int attackLevel;
        private int defenseLevel;
        private int strengthLevel;
        private int healthLevel;
        private int magicLevel;
        private int rangedLevel;
        private int woodcuttingLevel;
        private int fishingLevel;
        private int miningLevel;
        private int craftingLevel;
        private int cookingLevel;
        private int farmingLevel;
        private int slayerLevel;
        private int sailingLevel;
        private int healingLevel;

        public double Attack { get => attack; set => Set(ref attack, value); }
        public double Defense { get => defense; set => Set(ref defense, value); }
        public double Strength { get => strength; set => Set(ref strength, value); }
        public double Health { get => health; set => Set(ref health, value); }
        public double Woodcutting { get => woodcutting; set => Set(ref woodcutting, value); }
        public double Fishing { get => fishing; set => Set(ref fishing, value); }
        public double Mining { get => mining; set => Set(ref mining, value); }
        public double Crafting { get => crafting; set => Set(ref crafting, value); }
        public double Cooking { get => cooking; set => Set(ref cooking, value); }
        public double Farming { get => farming; set => Set(ref farming, value); }
        public double Slayer { get => slayer; set => Set(ref slayer, value); }
        public double Magic { get => magic; set => Set(ref magic, value); }
        public double Ranged { get => ranged; set => Set(ref ranged, value); }
        public double Sailing { get => sailing; set => Set(ref sailing, value); }
        public double Healing { get => healing; set => Set(ref healing, value); }
        public int AttackLevel { get => attackLevel; set => Set(ref attackLevel, value); }
        public int DefenseLevel { get => defenseLevel; set => Set(ref defenseLevel, value); }
        public int StrengthLevel { get => strengthLevel; set => Set(ref strengthLevel, value); }
        public int HealthLevel { get => healthLevel; set => Set(ref healthLevel, value); }
        public int WoodcuttingLevel { get => woodcuttingLevel; set => Set(ref woodcuttingLevel, value); }
        public int FishingLevel { get => fishingLevel; set => Set(ref fishingLevel, value); }
        public int MiningLevel { get => miningLevel; set => Set(ref miningLevel, value); }
        public int CraftingLevel { get => craftingLevel; set => Set(ref craftingLevel, value); }
        public int CookingLevel { get => cookingLevel; set => Set(ref cookingLevel, value); }
        public int FarmingLevel { get => farmingLevel; set => Set(ref farmingLevel, value); }
        public int SlayerLevel { get => slayerLevel; set => Set(ref slayerLevel, value); }
        public int MagicLevel { get => magicLevel; set => Set(ref magicLevel, value); }
        public int RangedLevel { get => rangedLevel; set => Set(ref rangedLevel, value); }
        public int SailingLevel { get => sailingLevel; set => Set(ref sailingLevel, value); }
        public int HealingLevel { get => healingLevel; set => Set(ref healingLevel, value); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int IndexOf(string skillName)
        {
            return SkillNames.IndexOf(skillName, StringComparison.OrdinalIgnoreCase);
        }

        public int GetLevel(int skillIndex)
        {
            var name = SkillNames[skillIndex];
            if (!expProperties.TryGetValue(name, out var expProp))
                expProp = EnsureDictionaries(name);
            levelProperties.TryGetValue(name, out var lvlProp);

            return (int)lvlProp.GetValue(this);
        }
        public int GetLevel(string skill)
        {
            var name = skill;
            if (!expProperties.TryGetValue(name, out _)) EnsureDictionaries(skill);
            levelProperties.TryGetValue(skill, out var lvlProp);
            if (lvlProp == null) return 0;
            return (int)lvlProp.GetValue(this);
        }

        public double GetExperience(int skillIndex)
        {
            var name = SkillNames[skillIndex];
            if (!expProperties.TryGetValue(name, out var expProp))
                expProp = EnsureDictionaries(name);

            return (double)expProp.GetValue(this);
        }
        public void Set(int skillIndex, int level, double exp, bool overrideLevel = false)
        {
            var name = SkillNames[skillIndex];
            if (!expProperties.TryGetValue(name, out var expProp))
                expProp = EnsureDictionaries(name);

            levelProperties.TryGetValue(name, out var lvlProp);

            if (overrideLevel)
            {
                lvlProp.SetValue(this, level);
                expProp.SetValue(this, exp);
                return;
            }

            var curLevel = (int)lvlProp.GetValue(this);
            var curExp = (double)expProp.GetValue(this);

            if (level > curLevel)
            {
                lvlProp.SetValue(this, level);
                expProp.SetValue(this, exp);
            }
            else if (level == curLevel && exp > curExp)
            {
                expProp.SetValue(this, exp);
            }
        }

        public IReadOnlyList<StatsUpdater> GetSkills()
        {
            return (skills ?? (skills = SkillNames.Select(GetSkill).ToList()));
        }

        public StatsUpdater GetSkill(int skillIndex)
        {
            return GetSkill(SkillNames[skillIndex]);
        }

        //public StatsUpdater this[string skillName]
        //{
        //    get
        //    {
        //        return GetSkill(skillName);
        //    }
        //}

        public StatsUpdater GetSkill(string name)
        {
            if (!expProperties.TryGetValue(name, out var expProp))
                expProp = EnsureDictionaries(name);

            levelProperties.TryGetValue(name, out var lvlProp);

            if (expProp == null || lvlProp == null)
            {
                return null;
            }

            return new StatsUpdater(this, Array.IndexOf(SkillNames, name), name, expProp, lvlProp);

            //var currentLevel = (int)lvlProp.GetValue(this);
            //if (currentLevel > 0)
            //    return ;
        }

        private PropertyInfo EnsureDictionaries(string name)
        {
            var props = typeof(Skills).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            PropertyInfo output = null;

            for (var i = 0; i < props.Length; ++i)
            {
                if (props[i].Name == nameof(Id))
                    continue;
                if (props[i].Name.EndsWith("Level"))
                {
                    levelProperties[props[i].Name.Replace("Level", "")] = props[i];
                    continue;
                }

                expProperties[props[i].Name] = props[i];

                if (props[i].Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    output = props[i];
            }
            return output;
        }
    }

    public class StatsUpdater
    {
        private readonly Skills source;
        private readonly PropertyInfo expProp;
        private readonly PropertyInfo lvlProp;

        public StatsUpdater(Skills source, int skillIndex, string name, PropertyInfo expProp, PropertyInfo lvlProp)
        {
            this.Name = name;
            this.Index = skillIndex;
            this.source = source;
            this.expProp = expProp;
            this.lvlProp = lvlProp;
        }
        public Skills Skills => source;
        public string Name { get; }
        public int Index { get; }
        public int Level
        {
            get => (int)lvlProp.GetValue(source);
            set => lvlProp.SetValue(source, value);
        }

        public double Experience
        {
            get => (double)expProp.GetValue(source);
            set => expProp.SetValue(source, value);
        }

        public override string ToString()
        {
            return "[" + Index + "] " + Name + " " + Level;
        }
    }
}


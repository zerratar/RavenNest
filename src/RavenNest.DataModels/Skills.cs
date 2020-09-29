using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace RavenNest.DataModels
{
    public partial class Skills : Entity<Skills>
    {
        private static readonly string[] skillNames = new string[] {
            nameof(Attack), nameof(Defense), nameof(Strength),
            nameof(Health), nameof(Woodcutting), nameof(Fishing),
            nameof(Mining), nameof(Crafting), nameof(Cooking),
            nameof(Farming), nameof(Slayer), nameof(Magic),
            nameof(Ranged), nameof(Sailing),
        };

        private static ConcurrentDictionary<string, PropertyInfo> expProperties = new ConcurrentDictionary<string, PropertyInfo>();
        private static ConcurrentDictionary<string, PropertyInfo> levelProperties = new ConcurrentDictionary<string, PropertyInfo>();

        private Guid id;
        private decimal attack;
        private decimal defense;
        private decimal strength;
        private decimal health;
        private decimal magic;
        private decimal ranged;
        private decimal woodcutting;
        private decimal fishing;
        private decimal mining;
        private decimal crafting;
        private decimal cooking;
        private decimal farming;
        private decimal slayer;
        private decimal sailing;
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


        public Guid Id { get => id; set => Set(ref id, value); }
        public decimal Attack { get => attack; set => Set(ref attack, value); }
        public decimal Defense { get => defense; set => Set(ref defense, value); }
        public decimal Strength { get => strength; set => Set(ref strength, value); }
        public decimal Health { get => health; set => Set(ref health, value); }
        public decimal Woodcutting { get => woodcutting; set => Set(ref woodcutting, value); }
        public decimal Fishing { get => fishing; set => Set(ref fishing, value); }
        public decimal Mining { get => mining; set => Set(ref mining, value); }
        public decimal Crafting { get => crafting; set => Set(ref crafting, value); }
        public decimal Cooking { get => cooking; set => Set(ref cooking, value); }
        public decimal Farming { get => farming; set => Set(ref farming, value); }
        public decimal Slayer { get => slayer; set => Set(ref slayer, value); }
        public decimal Magic { get => magic; set => Set(ref magic, value); }
        public decimal Ranged { get => ranged; set => Set(ref ranged, value); }
        public decimal Sailing { get => sailing; set => Set(ref sailing, value); }

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

        public int GetLevel(int skillIndex) 
        {
             var name = skillNames[skillIndex];
             if (!expProperties.TryGetValue(name, out var expProp))
                expProp = EnsureDictionaries(name);
            levelProperties.TryGetValue(name, out var lvlProp);

            return (int)lvlProp.GetValue(this);
        }

        public void SetLevel(int skillIndex, int level, decimal exp)
        {
            var name = skillNames[skillIndex];
            if (!expProperties.TryGetValue(name, out var expProp))
                expProp = EnsureDictionaries(name);

            levelProperties.TryGetValue(name, out var lvlProp);

            var curLevel = (int)lvlProp.GetValue(this);
            var curExp   = (decimal)expProp.GetValue(this);
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
            return skillNames.Select(GetSkill).ToList();
        }

        private StatsUpdater GetSkill(string name)
        {
            if (!expProperties.TryGetValue(name, out var expProp))
                expProp = EnsureDictionaries(name);

            levelProperties.TryGetValue(name, out var lvlProp);

            return new StatsUpdater(this, expProp, lvlProp);

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

        public StatsUpdater(Skills source, PropertyInfo expProp, PropertyInfo lvlProp)
        {
            this.source = source;
            this.expProp = expProp;
            this.lvlProp = lvlProp;
        }

        public int Level
        {
            get => (int)lvlProp.GetValue(source);
            set => lvlProp.SetValue(source, value);
        }

        public decimal Experience
        {
            get => (decimal)expProp.GetValue(source);
            set => expProp.SetValue(source, value);
        }
    }
}


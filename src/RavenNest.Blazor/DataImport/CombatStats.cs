using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace RavenNest
{
    public class CombatStats : IComparable<CombatStats>
    {
        public bool IsDead => this.Health.CurrentValue <= 0;

        public int Level => (int)((Health.Level + Attack.Level + Defense.Level + Strength.Level) / 4f);

        public SkillStat Health = new SkillStat
        {
            Name = "Health",
            Experience = 1154,
            CurrentValue = 10,
            Level = 10
        };

        public SkillStat Attack = new SkillStat
        {
            Name = "Attack",
            CurrentValue = 1,
            Level = 1
        };

        public SkillStat Defense = new SkillStat
        {
            Name = "Defense",
            CurrentValue = 1,
            Level = 1
        };

        public SkillStat Strength = new SkillStat
        {
            Name = "Strength",
            CurrentValue = 1,
            Level = 1
        };

        public SkillStat Magic = new SkillStat
        {
            Name = "Magic",
            CurrentValue = 1,
            Level = 1
        };

        public void UpdateCombatStats(IReadOnlyList<ItemDefinition> equipment)
        {
            WeaponPower = 0;
            WeaponAim = 0;
            ArmorPower = 0;
            foreach (var equip in equipment)
            {
                WeaponPower += equip.WeaponPower;
                WeaponAim += equip.WeaponAim;
                ArmorPower += equip.ArmorPower;
            }
        }

        public double ExpOverTime = 1;

        [JsonIgnore] public int WeaponPower = 0;
        [JsonIgnore] public int WeaponAim = 0;
        [JsonIgnore] public int ArmorPower = 0;

        public int CompareTo(CombatStats other)
        {
            if (ReferenceEquals(this, other)) return 0;
            if (ReferenceEquals(null, other)) return 1;

            return ExpOverTime.CompareTo(other.ExpOverTime) +
                   WeaponPower.CompareTo(other.WeaponPower) +
                   WeaponAim.CompareTo(other.WeaponAim) +
                   ArmorPower.CompareTo(other.ArmorPower);
        }
    }
}

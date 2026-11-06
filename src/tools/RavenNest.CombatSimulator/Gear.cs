using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace RavenNest.CombatSimulator
{
    /// <summary>An item from the public item list, with the stats combat reads today.</summary>
    public sealed class ItemData
    {
        public string Name { get; set; }
        public int Level { get; set; }
        public int Type { get; set; }
        public int WeaponAim { get; set; }
        public int WeaponPower { get; set; }
        public int MagicAim { get; set; }
        public int MagicPower { get; set; }
        public int RangedAim { get; set; }
        public int RangedPower { get; set; }
        public int ArmorPower { get; set; }
        public int RequiredAttackLevel { get; set; }
        public int RequiredDefenseLevel { get; set; }
        public int RequiredMagicLevel { get; set; }
        public int RequiredRangedLevel { get; set; }
    }

    /// <summary>What a fighter wears, summed the way the game sums equipment (Core/EquipmentStats.cs).</summary>
    public sealed class Loadout
    {
        public readonly List<ItemData> Items = new List<ItemData>();
        public int WeaponAim => Items.Sum(x => x.WeaponAim);
        public int WeaponPower => Items.Sum(x => x.WeaponPower);
        public int MagicAim => Items.Sum(x => x.MagicAim);
        public int MagicPower => Items.Sum(x => x.MagicPower);
        public int RangedAim => Items.Sum(x => x.RangedAim);
        public int RangedPower => Items.Sum(x => x.RangedPower);
        public int ArmorPower => Items.Sum(x => x.ArmorPower);

        public override string ToString() => string.Join(", ", Items.Select(x => x.Name));
    }

    /// <summary>Picks the best gear a character of a level can wear, from the public item list.</summary>
    public sealed class GearPicker
    {
        // RavenNest.Models.ItemType
        private static readonly int[] MeleeWeapons = { 1, 2, 3, 4, 7 };
        private const int Staff = 5, Bow = 6, Shield = 13;
        private static readonly int[] Armour = { 8, 9, 10, 11, 12, 16, 17 };

        private readonly List<ItemData> items;

        public GearPicker(string path)
        {
            var json = File.ReadAllText(path);
            items = JsonSerializer.Deserialize<List<ItemData>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }

        public int Count => items.Count;

        /// <summary>The best melee kit (weapon, shield and armour) wearable at <paramref name="level"/> in every combat skill.</summary>
        public Loadout Melee(int level)
        {
            var loadout = new Loadout();
            Add(loadout, items.Where(x => MeleeWeapons.Contains(x.Type) && x.RequiredAttackLevel <= level), x => x.WeaponPower * 2 + x.WeaponAim);
            Add(loadout, items.Where(x => x.Type == Shield && x.RequiredDefenseLevel <= level), x => x.ArmorPower);
            foreach (var type in Armour)
                Add(loadout, items.Where(x => x.Type == type && x.RequiredDefenseLevel <= level), x => x.ArmorPower * 2 + x.WeaponPower + x.WeaponAim);
            return loadout;
        }

        public Loadout Ranged(int level)
        {
            var loadout = new Loadout();
            Add(loadout, items.Where(x => x.Type == Bow && x.RequiredRangedLevel <= level), x => x.RangedPower * 2 + x.RangedAim);
            foreach (var type in Armour)
                Add(loadout, items.Where(x => x.Type == type && x.RequiredDefenseLevel <= level), x => x.ArmorPower * 2 + x.RangedPower + x.RangedAim);
            return loadout;
        }

        public Loadout Magic(int level)
        {
            var loadout = new Loadout();
            Add(loadout, items.Where(x => x.Type == Staff && x.RequiredMagicLevel <= level), x => x.MagicPower * 2 + x.MagicAim);
            foreach (var type in Armour)
                Add(loadout, items.Where(x => x.Type == type && x.RequiredDefenseLevel <= level), x => x.ArmorPower * 2 + x.MagicPower + x.MagicAim);
            return loadout;
        }

        private static void Add(Loadout loadout, IEnumerable<ItemData> candidates, Func<ItemData, int> score)
        {
            var best = candidates.OrderByDescending(score).ThenByDescending(x => x.Level).FirstOrDefault();
            if (best != null) loadout.Items.Add(best);
        }
    }
}

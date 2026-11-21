using RavenNest.Combat;

namespace RavenNest.BusinessLogic.Extensions
{
    /// <summary>
    /// An item's gear stats for combat v2 (the game's docs/combat-v2-design.md, sections 1 and 9): the classic
    /// columns (aim, power and armour per style) as accuracy, power and armour, plus <see cref="DataModels.Item.ExtraStats"/>
    /// for what the columns cannot hold. The columns stay the record for power, accuracy and armour while 0.9.6 games
    /// read them.
    /// </summary>
    public static class ItemGearStats
    {
        private static readonly GearStat[] Classic =
        {
            GearStat.MeleePower, GearStat.MeleeAccuracy,
            GearStat.RangedPower, GearStat.RangedAccuracy,
            GearStat.MagicPower, GearStat.MagicAccuracy,
            GearStat.Armor,
        };

        public static GearStats GearStats(this DataModels.Item item)
        {
            if (item == null) return new GearStats();
            var stats = Combat.GearStats.FromLegacy(item.WeaponAim, item.WeaponPower, item.RangedAim, item.RangedPower, item.MagicAim, item.MagicPower, item.ArmorPower);
            var extras = Combat.GearStats.Parse(item.ExtraStats);
            stats.Add(extras);
            stats.Unknown = extras.Unknown;
            return stats;
        }

        /// <summary>
        /// A full stat list (RavenNest.Models.Item.Stats, from an editor) without the stats the columns hold: what goes
        /// in <see cref="DataModels.Item.ExtraStats"/>. Null for an empty list.
        /// </summary>
        public static string ExtrasOf(string stats)
        {
            if (string.IsNullOrWhiteSpace(stats)) return null;
            var parsed = Combat.GearStats.Parse(stats);
            foreach (var stat in Classic) parsed[stat] = 0;
            return parsed.IsEmpty ? null : parsed.ToString();
        }

        /// <summary>
        /// The same stats said for people, one entry each: "+1.2% Magic Power", "+25% Fire Damage", "+14 Evasion".
        /// Empty for an item without them.
        /// </summary>
        public static System.Collections.Generic.IReadOnlyList<string> DescribeExtras(string stats)
        {
            var lines = new System.Collections.Generic.List<string>();
            if (string.IsNullOrWhiteSpace(stats)) return lines;
            var parsed = Combat.GearStats.Parse(stats);
            for (var i = 0; i < Combat.GearStats.Count; ++i)
            {
                var stat = (GearStat)i;
                if (System.Array.IndexOf(Classic, stat) >= 0) continue;
                var value = parsed[stat];
                if (value == 0) continue;
                var number = Combat.GearStats.IsShare(stat)
                    ? System.Math.Round(value * 100, 2).ToString(System.Globalization.CultureInfo.InvariantCulture) + "%"
                    : System.Math.Round(value, 2).ToString(System.Globalization.CultureInfo.InvariantCulture);
                lines.Add((value > 0 ? "+" : "") + number + " " + LabelOf(stat));
            }

            if (parsed.Unknown != null) lines.AddRange(parsed.Unknown);
            return lines;
        }

        // "+2% Magic Power" says it; "Magic Power Bonus" is the stat's name next to the flat Magic Power
        private static string LabelOf(GearStat stat)
        {
            switch (stat)
            {
                case GearStat.MeleePowerBonus: return "Melee Power";
                case GearStat.RangedPowerBonus: return "Ranged Power";
                case GearStat.MagicPowerBonus: return "Magic Power";
                case GearStat.HealingPowerBonus: return "Healing Power";
                case GearStat.AccuracyBonus: return "Accuracy";
                case GearStat.ArmorBonus: return "Armor";
                case GearStat.MaxHealthBonus: return "Max Health";
                default: return Combat.GearStats.Label(stat);
            }
        }
    }
}

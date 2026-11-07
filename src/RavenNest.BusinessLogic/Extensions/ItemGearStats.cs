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
    }
}

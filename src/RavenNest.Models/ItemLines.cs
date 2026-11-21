using System;

namespace RavenNest.Models
{
    /// <summary>The kind of armor an item is: the plate everyone knows, or one of the class lines (v0.9.7).</summary>
    public enum ItemLine
    {
        /// <summary>The 28 metal tiers, Bronze to Elder Atlarus: armor and nothing else.</summary>
        Plate = 0,
        /// <summary>Cloth for Magic: magic power, cast speed, element damage.</summary>
        Mage = 1,
        /// <summary>Cloth for Healing: healing power, heal shields, cooldowns.</summary>
        Healer = 2,
        /// <summary>Leather for Ranged: ranged power, crits, evasion.</summary>
        Ranger = 3,
        /// <summary>Mail for Attack and Strength: melee power, crit damage, attack speed.</summary>
        Melee = 4,
        /// <summary>Heavy plate for Defense: health, threat, damage reduction.</summary>
        Tank = 5,
    }

    /// <summary>
    /// Class gear (v0.9.7): armor lines for the paths a player takes, each of eight tiers, which trade armor for the
    /// stats of their path and look the part. A line's tiers are consecutive values of <see cref="ItemMaterial"/>
    /// after the metal tiers, so a material says both the line and the tier, and the game picks the colors by it.
    /// </summary>
    public static class ItemLines
    {
        public const int Tiers = 8;

        /// <summary>The level a tier's pieces need (in the line's skill), tier 1 first.</summary>
        public static readonly int[] TierLevels = { 1, 30, 70, 120, 200, 400, 650, 900 };

        /// <summary>The armor of a piece next to a plate piece of the same level and slot.</summary>
        public static double ArmorFactor(ItemLine line)
        {
            switch (line)
            {
                case ItemLine.Mage: return 0.4;
                case ItemLine.Healer: return 0.4;
                case ItemLine.Ranger: return 0.6;
                case ItemLine.Melee: return 0.75;
                default: return 1.0;
            }
        }

        public static bool IsClassGear(ItemMaterial material) => (int)material >= (int)ItemMaterial.Apprentice;

        public static ItemLine Of(ItemMaterial material)
        {
            var index = (int)material - (int)ItemMaterial.Apprentice;
            if (index < 0) return ItemLine.Plate;
            var line = 1 + index / Tiers;
            return line > (int)ItemLine.Tank ? ItemLine.Plate : (ItemLine)line;
        }

        /// <summary>1 to 8 for class gear, 0 for anything else.</summary>
        public static int TierOf(ItemMaterial material)
        {
            var index = (int)material - (int)ItemMaterial.Apprentice;
            return index < 0 || Of(material) == ItemLine.Plate ? 0 : 1 + index % Tiers;
        }

        public static ItemMaterial Material(ItemLine line, int tier)
        {
            if (line == ItemLine.Plate) throw new ArgumentException("Plate has its own tiers.", nameof(line));
            if (tier < 1 || tier > Tiers) throw new ArgumentOutOfRangeException(nameof(tier));
            return (ItemMaterial)((int)ItemMaterial.Apprentice + ((int)line - 1) * Tiers + tier - 1);
        }
    }
}

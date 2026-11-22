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

        // the second seven (2026-11-21). A new line is a new value here, its eight materials at the end of
        // ItemMaterial, and its entries in ItemLines.ArmorFactor and ItemLines.Fits: nothing else counts lines.
        /// <summary>Light leather for Attack: crits, attack speed, evasion.</summary>
        Rogue = 6,
        /// <summary>Hide and fur for Strength: melee power, crit damage, health.</summary>
        Barbarian = 7,
        /// <summary>Lacquered plates for Attack and Strength: melee power, accuracy, armor penetration.</summary>
        Samurai = 8,
        /// <summary>Blessed plate for Defense and Healing: health, healing power, threat.</summary>
        Paladin = 9,
        /// <summary>Dark plate for Defense and Strength: life steal, thorns, melee power.</summary>
        DarkKnight = 10,
        /// <summary>A hunter's coat for Ranged: ranged power, more against bosses and the undead.</summary>
        WitchHunter = 11,
        /// <summary>Wraps for Healing: healing power, cooldowns, regeneration, evasion.</summary>
        Monk = 12,

        /// <summary>
        /// Furs and hides for those who fight with a battle pet (2026-11-22): the pet's power, health and recovery, a
        /// better tame chance, and the wearer's own health and crits. Gated by Taming (<see cref="ItemLines.TamingLevelFor"/>)
        /// and fits Taming only, so "equip best" for a combat skill never picks it by itself.
        /// </summary>
        Beastmaster = 13,
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

        // every class line, in the order of their materials (the values 1, 2, 3, ... of ItemLine)
        private static readonly ItemLine[] all = BuildAll();

        private static ItemLine[] BuildAll()
        {
            var highest = 0;
            foreach (ItemLine line in Enum.GetValues(typeof(ItemLine)))
            {
                if ((int)line > highest) highest = (int)line;
            }
            var lines = new ItemLine[highest];
            for (var i = 0; i < highest; ++i) lines[i] = (ItemLine)(i + 1);
            return lines;
        }

        /// <summary>How many class lines there are (Plate is not one).</summary>
        public static int Count => all.Length;

        /// <summary>Every class line, the first one first. A copy: change it as you like.</summary>
        public static ItemLine[] All => (ItemLine[])all.Clone();

        /// <summary>The armor of a piece next to a plate piece of the same level and slot.</summary>
        public static double ArmorFactor(ItemLine line)
        {
            switch (line)
            {
                case ItemLine.Mage: return 0.4;
                case ItemLine.Healer: return 0.4;
                case ItemLine.Ranger: return 0.6;
                case ItemLine.Melee: return 0.75;
                case ItemLine.Rogue: return 0.6;
                case ItemLine.Barbarian: return 0.5;
                case ItemLine.Samurai: return 0.75;
                case ItemLine.Paladin: return 0.9;
                case ItemLine.DarkKnight: return 0.9;
                case ItemLine.WitchHunter: return 0.6;
                case ItemLine.Monk: return 0.4;
                case ItemLine.Beastmaster: return 0.6;
                default: return 1.0;
            }
        }

        public static bool IsClassGear(ItemMaterial material) => (int)material >= (int)ItemMaterial.Apprentice;

        /// <summary>
        /// The Taming level a piece needs: a Beastmaster piece its item level (the tier's level), anything else 0. The
        /// line is the first gear that Taming opens and the items have no column for it, so the rule is here, read by
        /// the game, the server and the website alike; the item's other requirement columns stay 0.
        /// </summary>
        public static int TamingLevelFor(ItemMaterial material, int itemLevel)
        {
            return IsClassGear(material) && Of(material) == ItemLine.Beastmaster ? Math.Max(1, itemLevel) : 0;
        }

        /// <summary>Is the material of this line; for <see cref="ItemLine.Plate"/>, anything that is not class gear.</summary>
        public static bool IsOf(ItemLine line, ItemMaterial material)
        {
            return line == ItemLine.Plate ? !IsClassGear(material) : IsClassGear(material) && Of(material) == line;
        }

        public static ItemLine Of(ItemMaterial material)
        {
            var index = (int)material - (int)ItemMaterial.Apprentice;
            if (index < 0) return ItemLine.Plate;
            var line = 1 + index / Tiers;
            return line > all.Length ? ItemLine.Plate : (ItemLine)line; // a line from a newer version
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

        /// <summary>The highest tier (1 to 8) whose level is at or below <paramref name="level"/>; 1 at least.</summary>
        public static int TierAt(int level)
        {
            var tier = 1;
            for (var i = 0; i < TierLevels.Length; ++i)
            {
                if (TierLevels[i] <= level) tier = i + 1;
            }
            return tier;
        }

        // ---- which lines fit a trained skill -------------------------------------------------------------------
        // Several lines can fit one skill (Attack: Melee, Rogue, Samurai), and a line can fit several skills
        // (Paladin: Defense and Healing). A set of lines is a number with one bit a line (Bit), 0 for none, so asking
        // costs nothing and no new type is needed in the game.

        /// <summary>A line's bit in a set of lines; 0 for Plate.</summary>
        public static int Bit(ItemLine line) => line == ItemLine.Plate ? 0 : 1 << (int)line;

        /// <summary>Is the line in the set.</summary>
        public static bool Has(int lines, ItemLine line) => line != ItemLine.Plate && (lines & (1 << (int)line)) != 0;

        /// <summary>
        /// The lines that fit a player training this skill, as a set (<see cref="Has"/>); 0 when none does. Health is
        /// what the game trains for "all" (and what the server keeps as "All"), so it names no line, and neither does
        /// a skill that is not combat.
        /// </summary>
        public static int Fits(Skill trained)
        {
            switch (trained)
            {
                case Skill.Magic: return Bit(ItemLine.Mage);
                case Skill.Healing: return Bit(ItemLine.Healer) | Bit(ItemLine.Paladin) | Bit(ItemLine.Monk);
                case Skill.Ranged: return Bit(ItemLine.Ranger) | Bit(ItemLine.WitchHunter);
                case Skill.Attack: return Bit(ItemLine.Melee) | Bit(ItemLine.Rogue) | Bit(ItemLine.Samurai);
                case Skill.Strength: return Bit(ItemLine.Melee) | Bit(ItemLine.Barbarian) | Bit(ItemLine.Samurai) | Bit(ItemLine.DarkKnight);
                case Skill.Defense: return Bit(ItemLine.Tank) | Bit(ItemLine.Paladin) | Bit(ItemLine.DarkKnight);
                // nobody trains Taming as a task (!tame keeps the combat skill), so this is for "!equip taming" and an
                // outfit of Taming; a combat skill's "equip best" never counts the Beastmaster line as fitting
                case Skill.Taming: return Bit(ItemLine.Beastmaster);
                default: return 0;
            }
        }

        /// <summary>Does the line fit a player training this skill.</summary>
        public static bool Fits(ItemLine line, Skill trained) => Has(Fits(trained), line);

        /// <summary>The lines of a set, the first one first.</summary>
        public static ItemLine[] In(int lines)
        {
            var count = 0;
            for (var i = 0; i < all.Length; ++i) if (Has(lines, all[i])) count++;
            var result = new ItemLine[count];
            var n = 0;
            for (var i = 0; i < all.Length; ++i) if (Has(lines, all[i])) result[n++] = all[i];
            return result;
        }
    }
}

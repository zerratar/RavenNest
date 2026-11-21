using System;

namespace RavenNest.Models
{
    /// <summary>
    /// How much a piece of gear is worth when the best one is picked ("equip all", an item that arrives and may
    /// replace what is worn). The one rule the server and the game both use (v0.9.7).
    ///
    /// Before class gear the worth was the sum of the seven classic columns (aim and power per style, armor), with
    /// whatever enchantments add: that is still what each side works out first and passes in here, and it is still the
    /// worth of everything that is not class gear. Class gear trades armor for the stats of a path, so by that sum it
    /// would always lose to plate. Here it is ranked by whether its line fits what the player is doing:
    ///  - a piece whose line fits counts as the plate piece of its level would (its armor divided by the line's
    ///    <see cref="ItemLines.ArmorFactor"/>), plus <see cref="FitMargin"/>: it beats plate of its level and the next
    ///    metals up, and loses to plate many tiers above;
    ///  - a piece that does not fit counts as its own, lower armor; the Tank line, which has the armor of plate, counts
    ///    as <see cref="UnfitTankShare"/> of it, so nobody ends up in threat gear by accident.
    /// What the player is doing is the skill being trained (<see cref="LineFor(Skill)"/>). When that names no line
    /// (training all, or a skill that is not combat), the line of the class piece already worn in the slot is the one
    /// that fits: what a player wears is kept, and plain armor is the fallback.
    /// Only the ranking is decided here; when an item is replaced is up to the caller, as before.
    /// </summary>
    public static class GearScore
    {
        /// <summary>The share a fitting class gear piece counts for above a plate piece of its level.</summary>
        public const double FitMargin = 0.25;

        /// <summary>What Tank gear that does not fit counts for, next to a plate piece with the same armor.</summary>
        public const double UnfitTankShare = 0.95;

        /// <summary>
        /// The line the trained skill calls for; <see cref="ItemLine.Plate"/> when none does. Health is what the game
        /// trains for "all" (and what the server keeps as "All"), so it names no line.
        /// </summary>
        public static ItemLine LineFor(Skill trained)
        {
            switch (trained)
            {
                case Skill.Magic: return ItemLine.Mage;
                case Skill.Healing: return ItemLine.Healer;
                case Skill.Ranged: return ItemLine.Ranger;
                case Skill.Attack:
                case Skill.Strength: return ItemLine.Melee;
                case Skill.Defense: return ItemLine.Tank;
                default: return ItemLine.Plate;
            }
        }

        /// <summary>
        /// The same from the task the server keeps for a character: the task "Fighting" and its argument (Attack,
        /// Defense, Strength, Ranged, Magic, Healing or All; the short forms players type are understood too).
        /// </summary>
        public static ItemLine LineFor(string task, string taskArgument)
        {
            if (string.IsNullOrEmpty(task) || string.IsNullOrEmpty(taskArgument)) return ItemLine.Plate;
            if (!task.Trim().Equals("Fighting", StringComparison.OrdinalIgnoreCase)) return ItemLine.Plate;
            var argument = taskArgument.Trim().ToLowerInvariant();
            if (argument.StartsWith("mag")) return ItemLine.Mage;
            if (argument.StartsWith("heal")) return ItemLine.Healer;
            if (argument.StartsWith("rang")) return ItemLine.Ranger;
            if (argument.StartsWith("att") || argument.StartsWith("atk") || argument.StartsWith("str")) return ItemLine.Melee;
            if (argument.StartsWith("def")) return ItemLine.Tank;
            return ItemLine.Plate;
        }

        /// <summary>
        /// The line that fits for one slot: the trained skill's, or, when that names none, the line of the class gear
        /// piece worn there now (<see cref="ItemMaterial.None"/> for an empty slot).
        /// </summary>
        public static ItemLine LineFor(ItemLine trained, ItemMaterial worn)
        {
            if (trained != ItemLine.Plate) return trained;
            return ItemLines.IsClassGear(worn) ? ItemLines.Of(worn) : ItemLine.Plate;
        }

        public static ItemLine LineFor(Skill trained, ItemMaterial worn) => LineFor(LineFor(trained), worn);

        /// <summary>The sum of the seven classic columns: what an item was worth before class gear.</summary>
        public static int Classic(Item item)
        {
            if (item == null) return 0;
            return item.WeaponAim + item.WeaponPower + item.ArmorPower + item.MagicAim + item.MagicPower + item.RangedAim + item.RangedPower;
        }

        /// <summary>
        /// An item's worth. <paramref name="classic"/> is the worth by the old rule (the classic columns, with the
        /// enchantments the caller counts); <paramref name="fits"/> is the line that fits (<see cref="LineFor(Skill, ItemMaterial)"/>).
        /// </summary>
        public static int Value(ItemMaterial material, int classic, ItemLine fits)
        {
            if (classic <= 0 || !ItemLines.IsClassGear(material)) return classic;
            var line = ItemLines.Of(material);
            if (line == ItemLine.Plate) return classic; // a line this version does not know
            if (line == fits)
                return (int)Math.Round(classic / ItemLines.ArmorFactor(line) * (1 + FitMargin), MidpointRounding.AwayFromZero);
            if (line == ItemLine.Tank)
                return Math.Min(classic - 1, (int)Math.Floor(classic * UnfitTankShare));
            return classic;
        }

        public static int Value(Item item, ItemLine fits) => item == null ? 0 : Value(item.Material, Classic(item), fits);
    }
}

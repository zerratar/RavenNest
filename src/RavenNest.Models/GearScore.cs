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
    ///  - a piece that does not fit counts as its own, lower armor; a line with the armor of plate (the Tank's) counts
    ///    as <see cref="UnfitShare"/> of it, so nobody ends up in threat gear by accident.
    /// What the player is doing is the skill being trained, and several lines can fit one skill
    /// (<see cref="ItemLines.Fits(Skill)"/>: a set of lines). When the skill names no line (training all, or a skill
    /// that is not combat), the line of the class piece already worn in the slot is the one that fits: what a player
    /// wears is kept, and plain armor is the fallback.
    /// Between two fitting pieces of different lines and the same tier the one already worn stays
    /// (<see cref="Beats"/>): which of two paths' gear to wear is the player's choice, not the ranking's.
    /// Only the ranking is decided here; when an item is replaced is up to the caller, as before.
    ///
    /// The game loads this library on its own runtime: nothing here may use a type that runtime lacks
    /// (MidpointRounding made EquipBestItems throw a TypeLoadException in the first play test). Plain numbers only.
    /// </summary>
    public static class GearScore
    {
        /// <summary>The share a fitting class gear piece counts for above a plate piece of its level.</summary>
        public const double FitMargin = 0.25;

        /// <summary>
        /// What class gear that does not fit counts for at most, next to a plate piece of its level: a line with
        /// more armor than this share of plate (the Tank's has all of it) is counted down to it.
        /// </summary>
        public const double UnfitShare = 0.95;

        /// <summary>The lines that fit the trained skill, as a set (<see cref="ItemLines.Has"/>); 0 when none does.</summary>
        public static int FitsFor(Skill trained) => ItemLines.Fits(trained);

        /// <summary>
        /// The skill a character trains, from the task the server keeps for it: the task "Fighting" and its argument
        /// (Attack, Defense, Strength, Ranged, Magic, Healing or All; the short forms players type are understood
        /// too). <see cref="Skill.None"/> for anything else.
        /// </summary>
        public static Skill Trained(string task, string taskArgument)
        {
            if (string.IsNullOrEmpty(task) || string.IsNullOrEmpty(taskArgument)) return Skill.None;
            if (!task.Trim().Equals("Fighting", StringComparison.OrdinalIgnoreCase)) return Skill.None;
            var argument = taskArgument.Trim().ToLowerInvariant();
            if (argument.StartsWith("mag")) return Skill.Magic;
            if (argument.StartsWith("heal")) return Skill.Healing;
            if (argument.StartsWith("rang")) return Skill.Ranged;
            if (argument.StartsWith("att") || argument.StartsWith("atk")) return Skill.Attack;
            if (argument.StartsWith("str")) return Skill.Strength;
            if (argument.StartsWith("def")) return Skill.Defense;
            return Skill.None;
        }

        /// <summary>The lines that fit, from the server's task for a character.</summary>
        public static int FitsFor(string task, string taskArgument) => ItemLines.Fits(Trained(task, taskArgument));

        /// <summary>
        /// The lines that fit for one slot: the trained skill's, or, when that names none, the line of the class gear
        /// piece worn there now (<see cref="ItemMaterial.None"/> for an empty slot).
        /// </summary>
        public static int KeepWorn(int trainedFits, ItemMaterial worn)
        {
            if (trainedFits != 0) return trainedFits;
            return ItemLines.IsClassGear(worn) ? ItemLines.Bit(ItemLines.Of(worn)) : 0;
        }

        /// <summary>The lines that fit for one slot, from the trained skill and what is worn there.</summary>
        public static int FitsFor(Skill trained, ItemMaterial worn) => KeepWorn(ItemLines.Fits(trained), worn);

        /// <summary>The sum of the seven classic columns: what an item was worth before class gear.</summary>
        public static int Classic(Item item)
        {
            if (item == null) return 0;
            return item.WeaponAim + item.WeaponPower + item.ArmorPower + item.MagicAim + item.MagicPower + item.RangedAim + item.RangedPower;
        }

        /// <summary>
        /// An item's worth. <paramref name="classic"/> is the worth by the old rule (the classic columns, with the
        /// enchantments the caller counts); <paramref name="fits"/> is the set of lines that fit
        /// (<see cref="FitsFor(Skill, ItemMaterial)"/>).
        /// </summary>
        public static int Value(ItemMaterial material, int classic, int fits)
        {
            if (classic <= 0 || !ItemLines.IsClassGear(material)) return classic;
            var line = ItemLines.Of(material);
            if (line == ItemLine.Plate) return classic; // a line this version does not know
            var factor = ItemLines.ArmorFactor(line);
            if (ItemLines.Has(fits, line))
                // a half rounds up, written without MidpointRounding (see the note on the class)
                return (int)Math.Floor(classic / factor * (1 + FitMargin) + 0.5);
            if (factor >= UnfitShare)
                return Math.Min(classic - 1, (int)Math.Floor(classic / factor * UnfitShare));
            return classic;
        }

        public static int Value(Item item, int fits) => item == null ? 0 : Value(item.Material, Classic(item), fits);

        /// <summary>
        /// Should the candidate replace what is worn. The higher <see cref="Value(ItemMaterial, int, int)"/> wins,
        /// with one exception: two fitting class pieces of different lines and the same tier are a choice of path
        /// (a Paladin's or a Tank's for someone training Defense), and there the worn one stays. Nothing worn is
        /// <see cref="ItemMaterial.None"/> with a worth of 0.
        /// </summary>
        public static bool Beats(ItemMaterial candidate, int candidateClassic, ItemMaterial worn, int wornClassic, int fits)
        {
            if (ItemLines.IsClassGear(candidate) && ItemLines.IsClassGear(worn))
            {
                var a = ItemLines.Of(candidate);
                var b = ItemLines.Of(worn);
                if (a != b && ItemLines.Has(fits, a) && ItemLines.Has(fits, b) && ItemLines.TierOf(candidate) == ItemLines.TierOf(worn))
                    return false;
            }
            return Value(candidate, candidateClassic, fits) > Value(worn, wornClassic, fits);
        }
    }
}

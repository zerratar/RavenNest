using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RavenNest.Combat
{
    /// <summary>
    ///     The stats a piece of gear can carry (the game's docs/combat-v2-design.md, sections 1 and 9). Today's
    ///     aim and power per style become accuracy and power, ArmorPower becomes armour, and gear can carry what
    ///     the old columns could not: element damage, resistances, evasion, health and crits.
    /// </summary>
    public enum GearStat
    {
        MeleePower,
        MeleeAccuracy,
        RangedPower,
        RangedAccuracy,
        MagicPower,
        MagicAccuracy,
        HealingPower,
        Armor,
        Evasion,
        MaxHealth,
        /// <summary>A share added to the crit chance (0.05 is 5 points more).</summary>
        CritChance,
        /// <summary>A share added to the crit multiplier (0.25 makes 150% into 175%).</summary>
        CritDamage,
        /// <summary>On a weapon: the share of its hit that turns to fire. On other gear: fire added to every hit.</summary>
        FireDamage,
        IceDamage,
        LightningDamage,
        PoisonDamage,
        /// <summary>A share taken off fire damage (players cap at 75%); negative is a weakness.</summary>
        FireResistance,
        IceResistance,
        LightningResistance,
        PoisonResistance,
    }

    /// <summary>
    ///     A list of gear stats with values, kept as text the way enchantments are
    ///     ("MELEE_POWER:120;MELEE_ACCURACY:95;FIRE_DAMAGE:30%"). Shares (element damage, resistances, crits) are
    ///     written as percentages and held as fractions (30% is 0.3). Stats that are not known (from a newer
    ///     server) are kept as they are, so nothing is lost passing through an older reader.
    /// </summary>
    [Serializable]
    public sealed class GearStats
    {
        public const int Count = (int)GearStat.PoisonResistance + 1;

        private static readonly string[] Keys;
        private static readonly Dictionary<string, GearStat> ByKey = new Dictionary<string, GearStat>(StringComparer.OrdinalIgnoreCase);

        static GearStats()
        {
            Keys = new string[Count];
            for (var i = 0; i < Count; ++i)
            {
                Keys[i] = KeyOf(((GearStat)i).ToString());
                ByKey[Keys[i]] = (GearStat)i;
            }

            // the old names, so a hand-written list with them still reads
            ByKey["WEAPON_POWER"] = GearStat.MeleePower;
            ByKey["WEAPON_AIM"] = GearStat.MeleeAccuracy;
            ByKey["MELEE_AIM"] = GearStat.MeleeAccuracy;
            ByKey["RANGED_AIM"] = GearStat.RangedAccuracy;
            ByKey["MAGIC_AIM"] = GearStat.MagicAccuracy;
            ByKey["ARMOR_POWER"] = GearStat.Armor;
            ByKey["ARMOUR"] = GearStat.Armor;
        }

        // MeleePower -> MELEE_POWER
        private static string KeyOf(string name)
        {
            var sb = new StringBuilder();
            for (var i = 0; i < name.Length; ++i)
            {
                if (i > 0 && char.IsUpper(name[i])) sb.Append('_');
                sb.Append(char.ToUpperInvariant(name[i]));
            }
            return sb.ToString();
        }

        public double[] Values = new double[Count];

        /// <summary>Entries this version does not know, kept for <see cref="ToString"/>.</summary>
        public List<string> Unknown;

        public double this[GearStat stat]
        {
            get => Values[(int)stat];
            set => Values[(int)stat] = value;
        }

        public bool IsEmpty
        {
            get
            {
                foreach (var v in Values) if (v != 0) return false;
                return Unknown == null || Unknown.Count == 0;
            }
        }

        /// <summary>Element damage, resistances and crits are shares, written as percentages.</summary>
        public static bool IsShare(GearStat stat) => stat >= GearStat.CritChance;

        /// <summary>The name for people (Melee Power, Fire Resistance).</summary>
        public static string Label(GearStat stat)
        {
            var name = stat.ToString();
            var sb = new StringBuilder();
            for (var i = 0; i < name.Length; ++i)
            {
                if (i > 0 && char.IsUpper(name[i])) sb.Append(' ');
                sb.Append(name[i]);
            }
            return sb.ToString();
        }

        /// <summary>The name in the text form (MELEE_POWER).</summary>
        public static string Key(GearStat stat) => Keys[(int)stat];

        public static bool TryParseKey(string key, out GearStat stat) => ByKey.TryGetValue(key?.Trim() ?? "", out stat);

        public static GearStat DamageOf(Element element)
        {
            switch (element)
            {
                case Element.Fire: return GearStat.FireDamage;
                case Element.Ice: return GearStat.IceDamage;
                case Element.Lightning: return GearStat.LightningDamage;
                case Element.Poison: return GearStat.PoisonDamage;
                default: throw new ArgumentException("Physical damage is power, not an element stat.", nameof(element));
            }
        }

        public static GearStat ResistanceOf(Element element)
        {
            switch (element)
            {
                case Element.Fire: return GearStat.FireResistance;
                case Element.Ice: return GearStat.IceResistance;
                case Element.Lightning: return GearStat.LightningResistance;
                case Element.Poison: return GearStat.PoisonResistance;
                default: throw new ArgumentException("Physical damage is reduced by armour, not a resistance stat.", nameof(element));
            }
        }

        /// <summary>The element damage shares.</summary>
        public ElementValues ElementDamage => new ElementValues
        {
            Fire = this[GearStat.FireDamage],
            Ice = this[GearStat.IceDamage],
            Lightning = this[GearStat.LightningDamage],
            Poison = this[GearStat.PoisonDamage],
        };

        public ElementValues Resistance => new ElementValues
        {
            Fire = this[GearStat.FireResistance],
            Ice = this[GearStat.IceResistance],
            Lightning = this[GearStat.LightningResistance],
            Poison = this[GearStat.PoisonResistance],
        };

        /// <summary>Power or accuracy for a style; healing has power only.</summary>
        public double Power(AttackStyle style)
        {
            switch (style)
            {
                case AttackStyle.Ranged: return this[GearStat.RangedPower];
                case AttackStyle.Magic: return this[GearStat.MagicPower];
                case AttackStyle.Healing: return this[GearStat.HealingPower];
                default: return this[GearStat.MeleePower];
            }
        }

        public double Accuracy(AttackStyle style)
        {
            switch (style)
            {
                case AttackStyle.Ranged: return this[GearStat.RangedAccuracy];
                case AttackStyle.Magic: return this[GearStat.MagicAccuracy];
                case AttackStyle.Healing: return 0;
                default: return this[GearStat.MeleeAccuracy];
            }
        }

        /// <summary>
        ///     The weapon's style, from which power it carries (most first: melee, ranged, magic, healing); null for
        ///     gear without one. A weapon's element damage converts the hits of this style.
        /// </summary>
        public AttackStyle? WeaponStyle()
        {
            AttackStyle? best = null;
            var most = 0.0;
            foreach (var style in new[] { AttackStyle.Melee, AttackStyle.Ranged, AttackStyle.Magic, AttackStyle.Healing })
            {
                var p = Power(style) + Accuracy(style);
                if (p > most) { most = p; best = style; }
            }
            return best;
        }

        /// <summary>
        ///     The stats from an item's classic columns (today's items): aim becomes accuracy, ArmorPower armour.
        ///     The migration in section 9 of the design.
        /// </summary>
        public static GearStats FromLegacy(int weaponAim, int weaponPower, int rangedAim, int rangedPower, int magicAim, int magicPower, int armorPower)
        {
            var s = new GearStats();
            s[GearStat.MeleeAccuracy] = weaponAim;
            s[GearStat.MeleePower] = weaponPower;
            s[GearStat.RangedAccuracy] = rangedAim;
            s[GearStat.RangedPower] = rangedPower;
            s[GearStat.MagicAccuracy] = magicAim;
            s[GearStat.MagicPower] = magicPower;
            s[GearStat.Armor] = armorPower;
            return s;
        }

        /// <summary>Reads the text form. Empty or null gives an empty list; a broken entry is skipped.</summary>
        public static GearStats Parse(string text)
        {
            var s = new GearStats();
            if (string.IsNullOrWhiteSpace(text)) return s;
            foreach (var part in text.Split(';'))
            {
                var entry = part.Trim();
                if (entry.Length == 0) continue;
                var colon = entry.IndexOf(':');
                if (colon <= 0)
                {
                    continue;
                }

                var key = entry.Substring(0, colon).Trim();
                var valueText = entry.Substring(colon + 1).Trim();
                var percent = valueText.EndsWith("%");
                if (percent) valueText = valueText.Substring(0, valueText.Length - 1).Trim();
                if (!double.TryParse(valueText.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                {
                    continue;
                }

                if (!TryParseKey(key, out var stat))
                {
                    (s.Unknown ??= new List<string>()).Add(entry);
                    continue;
                }

                // shares are written as percentages; a bare number for a share is taken as a fraction when small
                if (IsShare(stat) && (percent || Math.Abs(value) > 1)) value /= 100.0;
                else if (!IsShare(stat) && percent) continue; // a percentage of power means nothing on its own
                s.Values[(int)stat] += value;
            }
            return s;
        }

        /// <summary>The text form, stats in a fixed order, zeros left out.</summary>
        public override string ToString()
        {
            var sb = new StringBuilder();
            for (var i = 0; i < Count; ++i)
            {
                var v = Values[i];
                if (v == 0) continue;
                if (sb.Length > 0) sb.Append(';');
                sb.Append(Keys[i]).Append(':');
                if (IsShare((GearStat)i)) sb.Append(Math.Round(v * 100, 2).ToString(CultureInfo.InvariantCulture)).Append('%');
                else sb.Append(Math.Round(v, 2).ToString(CultureInfo.InvariantCulture));
            }

            if (Unknown != null)
            {
                foreach (var u in Unknown)
                {
                    if (sb.Length > 0) sb.Append(';');
                    sb.Append(u);
                }
            }
            return sb.ToString();
        }

        public GearStats Copy()
        {
            var c = new GearStats();
            Array.Copy(Values, c.Values, Count);
            if (Unknown != null) c.Unknown = new List<string>(Unknown);
            return c;
        }

        /// <summary>Adds another list (all worn gear), scaled.</summary>
        public void Add(GearStats other, double scale = 1)
        {
            if (other == null) return;
            for (var i = 0; i < Count; ++i) Values[i] += other.Values[i] * scale;
        }

        /// <summary>
        ///     Puts gear stats on a sheet: power and accuracy per style, armour, and everything in
        ///     <see cref="ApplyExtras"/>. For a whole set of worn gear, or a single piece.
        /// </summary>
        /// <param name="weaponStyle">The style whose hits the weapon's element damage converts; null adds it to every hit.</param>
        public void ApplyTo(CombatSheet sheet, AttackStyle? weaponStyle)
        {
            sheet.Melee.Power += (int)this[GearStat.MeleePower];
            sheet.Melee.Accuracy += (int)this[GearStat.MeleeAccuracy];
            sheet.Ranged.Power += (int)this[GearStat.RangedPower];
            sheet.Ranged.Accuracy += (int)this[GearStat.RangedAccuracy];
            sheet.Magic.Power += (int)this[GearStat.MagicPower];
            sheet.Magic.Accuracy += (int)this[GearStat.MagicAccuracy];
            sheet.Armor += (int)this[GearStat.Armor];
            ApplyExtras(sheet, weaponStyle);
        }

        /// <summary>
        ///     The stats the classic columns never had: healing power, evasion, health, crits, element damage and
        ///     resistances. The game adds these on top of its own power, accuracy and armour (which already carry the
        ///     enchantments).
        /// </summary>
        public void ApplyExtras(CombatSheet sheet, AttackStyle? weaponStyle)
        {
            sheet.Healing.Power += (int)this[GearStat.HealingPower];
            sheet.GearEvasion += (int)this[GearStat.Evasion];
            sheet.MaxHealth += (int)this[GearStat.MaxHealth];
            sheet.CritChance += this[GearStat.CritChance];
            sheet.CritDamage += this[GearStat.CritDamage];
            sheet.Resistance += Resistance;

            var element = ElementDamage;
            if (element.Total == 0) return;
            if (weaponStyle.HasValue) sheet.Style(weaponStyle.Value).Conversion += element;
            else sheet.ElementBonus += element;
        }
    }
}

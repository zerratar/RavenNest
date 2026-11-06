using System;
using System.Collections.Generic;

namespace RavenNest.Combat
{
    /// <summary>A blow's options from the skill that made it: how much harder, which element, what it ignores.</summary>
    public struct AttackOptions
    {
        /// <summary>The skill's damage multiplier; 0 is read as 1, so a default value is a plain attack.</summary>
        public double Multiplier;

        /// <summary>The skill turns the whole hit into this element (Fireball), or null to keep the weapon's split.</summary>
        public Element? Element;

        /// <summary>The share of the target's armour this blow ignores (Piercing Shot ignores half).</summary>
        public double ArmorIgnored;

        /// <summary>Skills that cannot miss (most heals and some boss attacks).</summary>
        public bool CannotMiss;

        public static readonly AttackOptions Basic = new AttackOptions { Multiplier = 1 };
    }

    public sealed class AttackResult
    {
        public bool Hit;
        public bool Critical;

        /// <summary>Damage dealt per element, after armour, resistances and multipliers.</summary>
        public ElementValues Damage;

        /// <summary>The whole blow, rounded, at least 1 when it hit and was not fully resisted.</summary>
        public int Total;

        /// <summary>Every element the target is immune to and that this blow carried.</summary>
        public bool Immune;

        /// <summary>The element effects that triggered (burn, chill, shock, poison), for the status effect system.</summary>
        public List<ElementProc> Procs = new List<ElementProc>();
    }

    public struct ElementProc
    {
        public Element Element;

        /// <summary>The damage the element dealt in the blow that triggered it; effects are sized from it.</summary>
        public double Damage;
    }

    public sealed class HealResult
    {
        public bool Critical;
        public int Amount;
    }

    /// <summary>
    ///     The rules of a blow (docs/combat-v2-design.md, sections 1 to 3), shared by the game and the simulator.
    ///     The constants are starting points; the combat simulator is what tunes them.
    /// </summary>
    public static class CombatMath
    {
        /// <summary>Health is ten times larger than the Health level was, so blows are ten times larger too.</summary>
        public const int HealthPerLevel = 10;
        public const double DamageScale = 10;

        public const double BaseCritChance = 0.05;
        public const double BaseCritDamage = 1.5;
        public const double MinHitChance = 0.05;
        public const double MaxHitChance = 0.95;
        public const double PlayerResistanceCap = 0.75;

        /// <summary>Armour against a level 0 attacker; every attacker level adds <see cref="ArmorPerAttackerLevel"/>.</summary>
        public const double ArmorBase = 100;
        public const double ArmorPerAttackerLevel = 25;

        /// <summary>An element's effect always has this chance when the element dealt damage, plus up to <see cref="ProcShareChance"/> by its share of the blow.</summary>
        public const double ProcBaseChance = 0.10;
        public const double ProcShareChance = 0.40;

        public static int MaxHealth(int healthLevel, int bonus = 0) => Math.Max(1, healthLevel * HealthPerLevel + bonus);

        /// <summary>
        ///     The biggest normal hit: today's MaxHit (Core/GameMath.cs) times ten, without its rounding, so progress
        ///     made before the new combat still means the same.
        /// </summary>
        public static double MaxHit(int damageLevel, int power) =>
            DamageScale * ((damageLevel + 3) * (power * 0.00175 + 0.1) + 1.05) * 0.95;

        public static int AccuracyRating(int aimLevel, int gearAccuracy) => Math.Max(0, 2 * aimLevel + gearAccuracy);

        public static int EvasionRating(int defenseLevel, int gearEvasion) => Math.Max(0, 2 * defenseLevel + gearEvasion);

        /// <summary>Even ratings hit half the time, twice the accuracy hits three in four, and nothing is ever certain.</summary>
        public static double HitChance(int accuracy, int evasion)
        {
            var scale = 2.0 * Math.Max(1, Math.Max(accuracy, evasion));
            return Clamp(0.5 + (accuracy - evasion) / scale, MinHitChance, MaxHitChance);
        }

        /// <summary>The share of physical damage armour takes off, measured against the attacker's level so it never makes anyone untouchable.</summary>
        public static double ArmorReduction(int armor, int attackerLevel)
        {
            if (armor <= 0) return 0;
            return armor / (armor + ArmorBase + ArmorPerAttackerLevel * Math.Max(0, attackerLevel));
        }

        /// <summary>What is left of an element's damage after resistance: players cap at 75%, enemies can be weak or immune.</summary>
        public static double ResistanceMultiplier(double resistance, bool isPlayer)
        {
            var capped = isPlayer ? Math.Min(resistance, PlayerResistanceCap) : Math.Min(resistance, 1);
            return Math.Max(0, 1 - Math.Max(capped, -1));
        }

        /// <summary>
        ///     A normal hit's size as a share of the max hit, leaning high: 5% max, 7% 91-100%, 73% 46-90%, the rest
        ///     26-50% (today's damage bands, without armour narrowing them, since armour now reduces damage).
        /// </summary>
        public static double RollHitShare(ICombatRandom random)
        {
            var band = random.Next();
            var spread = random.Next();
            if (band < 0.05) return 1.0;
            if (band < 0.12) return 0.91 + 0.09 * spread;
            if (band < 0.85) return 0.9 * (0.51 + 0.49 * spread);
            return 0.5 * (0.51 + 0.49 * spread);
        }

        /// <summary>
        ///     One blow from <paramref name="attacker"/> with <paramref name="style"/> on <paramref name="defender"/>.
        ///     Healing is not an attack; use <see cref="Heal"/>.
        /// </summary>
        public static AttackResult Attack(CombatSheet attacker, AttackStyle style, CombatSheet defender, ICombatRandom random, AttackOptions options = default)
        {
            var result = new AttackResult();
            var stats = attacker.Style(style);
            var multiplier = options.Multiplier > 0 ? options.Multiplier : 1;

            if (!options.CannotMiss)
            {
                var accuracy = AccuracyRating(stats.AimLevel, stats.Accuracy);
                var evasion = EvasionRating(defender.DefenseLevel, defender.GearEvasion);
                if (random.Next() >= HitChance(accuracy, evasion))
                    return result;
            }

            result.Hit = true;
            var raw = MaxHit(stats.DamageLevel, stats.Power) * RollHitShare(random) * multiplier * attacker.DamageMultiplier;
            if (random.Next() < attacker.CritChance)
            {
                result.Critical = true;
                raw *= Math.Max(1, attacker.CritDamage);
            }

            // split the blow: the skill's element takes all of it, otherwise the weapon converts its share, and
            // gems and passives add element damage on top
            var split = new ElementValues();
            if (options.Element.HasValue)
            {
                split[options.Element.Value] = 1;
            }
            else
            {
                var converted = Math.Min(1, Math.Max(0, stats.Conversion.Total - stats.Conversion.Physical));
                var scale = converted > 0 ? converted / (stats.Conversion.Total - stats.Conversion.Physical) : 0;
                foreach (var element in Elements.All)
                {
                    if (element == Element.Physical) continue;
                    split[element] = Math.Max(0, stats.Conversion[element]) * scale;
                }
                split.Physical = 1 - converted;
            }
            split += attacker.ElementBonus;
            split.Physical = Math.Max(0, split.Physical);

            var armorLeft = 1 - ArmorReduction((int)(defender.Armor * (1 - Clamp(options.ArmorIgnored, 0, 1))), attacker.CombatLevel);
            var carried = 0;
            var immune = 0;
            foreach (var element in Elements.All)
            {
                var share = split[element];
                if (share <= 0) continue;
                carried++;
                var amount = raw * share;
                if (element == Element.Physical) amount *= armorLeft;
                var left = ResistanceMultiplier(defender.Resistance[element], defender.IsPlayer);
                if (left <= 0) immune++;
                amount *= left * defender.DamageTakenMultiplier;
                result.Damage[element] = amount;
            }

            var total = result.Damage.Total;
            result.Immune = carried > 0 && immune == carried;
            result.Total = result.Immune ? 0 : Math.Max(1, (int)Math.Round(total));

            // element effects: a share of the blow in an element gives that element's effect a chance
            if (total > 0)
            {
                foreach (var element in Elements.All)
                {
                    if (element == Element.Physical) continue;
                    var dealt = result.Damage[element];
                    if (dealt <= 0) continue;
                    var chance = ProcBaseChance + ProcShareChance * (dealt / total);
                    if (random.Next() < chance)
                        result.Procs.Add(new ElementProc { Element = element, Damage = dealt });
                }
            }

            return result;
        }

        /// <summary>A heal from <paramref name="healer"/>: never misses, can crit, nothing reduces it.</summary>
        public static HealResult Heal(CombatSheet healer, ICombatRandom random, double multiplier = 1)
        {
            var stats = healer.Healing;
            var amount = MaxHit(stats.DamageLevel, stats.Power) * RollHitShare(random) * multiplier * healer.HealingMultiplier;
            var result = new HealResult();
            if (random.Next() < healer.CritChance)
            {
                result.Critical = true;
                amount *= Math.Max(1, healer.CritDamage);
            }

            result.Amount = Math.Max(1, (int)Math.Round(amount));
            return result;
        }

        public static double Clamp(double value, double min, double max) => value < min ? min : value > max ? max : value;
    }
}

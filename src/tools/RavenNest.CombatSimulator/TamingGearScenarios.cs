using RavenNest.Combat;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace RavenNest.CombatSimulator
{
    /// <summary>
    ///     Taming gear (the game's docs/taming-gear-design.md): the Beastmaster line against each style's damage line
    ///     (Melee, Ranger, Mage), tier by tier, with a damage pet out and with none. The aim from the design: with a
    ///     damage pet out the wearer and pet together land within about five percent of the same character in its
    ///     damage line (and the same pet); with no pet out, clearly behind.
    ///
    ///     The fighter is at the tier's level in every combat skill (tier 1 at level 10), with the standard weapon of
    ///     that level (and the shield for melee), and five armor pieces of the line: each the plate piece of its slot
    ///     times the line's armor factor, and the line's set total of stats (GameData.ClassGear's numbers, linear from
    ///     tier 1 to tier 8 in halves). The enemy never falls and is the same for both (the standard melee kit at its
    ///     level, 75% of its armor, half its power and accuracy, as in "pets"). The pet is the damage stand-in at the
    ///     fighter's level, evolved at 30 and 100; the Beastmaster set's pet stats reach it as the game gives them
    ///     (PetScenarios.Fight). No skill tree on either side.
    ///
    ///     Usage: taminggear [runs] [stat=from:to ...], for example "taminggear 200 PET_POWER=3:40" to try other
    ///     numbers for the Beastmaster set (keys as in the item text).
    /// </summary>
    public static class TamingGearScenarios
    {
        private static readonly int[] TierLevels = { 1, 30, 70, 120, 200, 400, 650, 900 };
        private static readonly int[] ArmourSlots = { 8, 9, 10, 11, 12 }; // helmet, chest, gloves, boots, leggings

        private sealed class Line
        {
            public string Name;
            public double ArmorFactor;
            public List<(GearStat Stat, double From, double To)> Stats = new List<(GearStat, double, double)>();
        }

        private static Line Melee => new Line { Name = "Melee", ArmorFactor = 0.75, Stats = { (GearStat.MeleePowerBonus, 2, 14), (GearStat.CritDamage, 3, 20), (GearStat.AttackSpeed, 1, 5), (GearStat.ArmorPenetration, 2, 12) } };
        private static Line Ranger => new Line { Name = "Ranger", ArmorFactor = 0.6, Stats = { (GearStat.RangedPowerBonus, 2, 14), (GearStat.CritChance, 0.5, 4), (GearStat.AttackSpeed, 1, 6) } };
        private static Line Mage => new Line { Name = "Mage", ArmorFactor = 0.4, Stats = { (GearStat.MagicPowerBonus, 2, 16), (GearStat.CastSpeed, 1, 8), (GearStat.AccuracyBonus, 1, 8) } };

        /// <summary>The Beastmaster line as GameData.ClassGear has it (keep the two the same).</summary>
        public static readonly List<(GearStat Stat, double From, double To)> BeastmasterStats = new List<(GearStat, double, double)>
        {
            (GearStat.MaxHealthBonus, 1, 8), (GearStat.CritChance, 0.5, 3),
            (GearStat.PetPower, 4, 75), (GearStat.PetHealth, 3, 20), (GearStat.PetRecovery, 2, 15),
            (GearStat.PetGathering, 1, 10), (GearStat.TameChance, 2, 15),
        };

        /// <summary>A set's total at a tier, in percent: linear from tier 1 to tier 8 in halves (GameData.ClassGearSetTotal).</summary>
        public static double SetTotal(double from, double to, int tier)
        {
            var raw = from + (to - from) * (tier - 1) / 7.0;
            return Math.Floor(raw * 2 + 0.5) / 2;
        }

        private static GearStats SetStats(IEnumerable<(GearStat Stat, double From, double To)> stats, int tier)
        {
            var s = new GearStats();
            foreach (var (stat, from, to) in stats) s[stat] = SetTotal(from, to, tier) / 100.0;
            return s;
        }

        public static void Run(GearPicker allGear, int runs, string[] overrides)
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            var beastmaster = new Line { Name = "Beastmaster", ArmorFactor = 0.6, Stats = new List<(GearStat, double, double)>(BeastmasterStats) };
            foreach (var o in overrides ?? new string[0])
            {
                var eq = o.IndexOf('=');
                var colon = o.IndexOf(':');
                if (eq <= 0 || colon <= eq || !GearStats.TryParseKey(o.Substring(0, eq), out var stat)) { Console.WriteLine("Not understood: " + o); continue; }
                var from = double.Parse(o.Substring(eq + 1, colon - eq - 1), CultureInfo.InvariantCulture);
                var to = double.Parse(o.Substring(colon + 1), CultureInfo.InvariantCulture);
                beastmaster.Stats.RemoveAll(x => x.Stat == stat);
                beastmaster.Stats.Add((stat, from, to));
            }

            var gear = allGear.Standard();
            var damagePet = BattlePets.Archetypes.First(a => a.Role == PetRole.Damage);
            Console.WriteLine("Taming gear: the Beastmaster set against each style's damage line, " + runs + " fights of 120 s per cell, no skill tree.");
            Console.WriteLine("Beastmaster set at tier 1 / tier 8: " + string.Join(", ", beastmaster.Stats.Select(x => GearStats.Label(x.Stat) + " " + x.From + "/" + x.To + "%")));
            Console.WriteLine("Damage pet: " + damagePet.NameAt(0) + ". 'alone' = the owner's damage, no pet; 'with pet' = owner and pet together.");
            Console.WriteLine("Ratios are Beastmaster over the damage line: the aim is about 0.95 to 1.05 with the pet, clearly below 1 alone.");
            Console.WriteLine();
            Console.WriteLine($"{"Tier",4} {"Lv",4} {"Style",6} | {"line alone",10} {"BM alone",9} {"ratio",6} | {"line+pet",9} {"BM+pet",9} {"ratio",6} | {"pet share",9} {"BM pet hp",9} {"KOs line/BM",12} | {"owner hp line/BM",17}");
            var random = new SeededCombatRandom(97);
            var summary = new List<string>();
            for (var tier = 1; tier <= 8; ++tier)
            {
                var level = Math.Max(10, TierLevels[tier - 1]);
                var stage = level >= BattlePets.EvolveLevels[1] ? 2 : level >= BattlePets.EvolveLevels[0] ? 1 : 0;
                var plateMelee = gear.Melee(level);
                var enemy = Enemy(plateMelee, level, 0.75, 0.5);
                foreach (var (style, line, plate) in new[] { (AttackStyle.Melee, Melee, plateMelee), (AttackStyle.Ranged, Ranger, gear.Ranged(level)), (AttackStyle.Magic, Mage, gear.Magic(level)) })
                {
                    var lineSheet = Owner(plate, level, style, line, tier);
                    var bmSheet = Owner(plate, level, style, beastmaster, tier);
                    var bond = SetStats(beastmaster.Stats.Where(x => x.Stat >= GearStat.PetPower), tier);

                    PetScenarios.Result Avg(CombatSheet owner, PetSpecies pet, GearStats b) =>
                        PetScenarios.Average(Enumerable.Range(0, runs).Select(_ => PetScenarios.Fight(owner.Copy(), style, enemy.Copy(), level, pet, stage, b, random)).ToList());

                    var lineAlone = Avg(lineSheet, null, null);
                    var bmAlone = Avg(bmSheet, null, null);
                    var linePet = Avg(lineSheet, damagePet, null);
                    var bmPet = Avg(bmSheet, damagePet, bond);
                    double Total(PetScenarios.Result r) => r.OwnerDamage + r.PetDamage;
                    var aloneRatio = bmAlone.OwnerDamage / Math.Max(1, lineAlone.OwnerDamage);
                    var petRatio = Total(bmPet) / Math.Max(1, Total(linePet));
                    var share = bmPet.PetDamage / Math.Max(1, Total(bmPet));
                    var petHp = (int)Math.Round(damagePet.Sheet(level, stage).MaxHealth * (1 + bond[GearStat.PetHealth]));
                    Console.WriteLine($"{tier,4} {level,4} {style,6} | {lineAlone.OwnerDamage / 120,10:0.0} {bmAlone.OwnerDamage / 120,9:0.0} {aloneRatio,6:0.00} | {Total(linePet) / 120,9:0.0} {Total(bmPet) / 120,9:0.0} {petRatio,6:0.00} | {share,9:0%} {petHp,9} {linePet.Knockouts / (double)runs,5:0.0}/{bmPet.Knockouts / (double)runs,-6:0.0} | {lineSheet.MaxHealth,8}/{bmSheet.MaxHealth,-8}");
                    summary.Add($"| {tier} | {level} | {line.Name} | {aloneRatio:0.00} | {petRatio:0.00} | {share:0%} |");
                }
            }
            Console.WriteLine();
            Console.WriteLine("For the doc (damage per second over the damage line's; pet share of the Beastmaster wearer's damage):");
            Console.WriteLine("| Tier | Level | Against | Alone | With a damage pet | Pet's share |");
            Console.WriteLine("|---|---|---|---|---|---|");
            foreach (var row in summary) Console.WriteLine(row);
        }

        private static CombatSheet Enemy(Loadout kit, int level, double armourShare, double offenceShare)
        {
            var enemy = new CombatSheet { CombatLevel = level, MaxHealth = int.MaxValue, DefenseLevel = level, Armor = (int)(kit.ArmorPower * armourShare) };
            enemy.Melee.DamageLevel = level;
            enemy.Melee.AimLevel = level;
            enemy.Melee.Power = (int)(kit.WeaponPower * offenceShare);
            enemy.Melee.Accuracy = (int)(kit.WeaponAim * offenceShare);
            return enemy;
        }

        /// <summary>A fighter in the plate kit's weapon (and shield) and five pieces of a line.</summary>
        private static CombatSheet Owner(Loadout plate, int level, AttackStyle style, Line line, int tier)
        {
            var kit = new Loadout();
            foreach (var item in plate.Items)
            {
                if (!ArmourSlots.Contains(item.Type)) { kit.Items.Add(item); continue; }
                kit.Items.Add(new ItemData { Name = line.Name + " " + item.Type, Type = item.Type, Level = item.Level, ArmorPower = (int)Math.Ceiling(item.ArmorPower * line.ArmorFactor - 0.000001), Stats = "" });
            }

            var sheet = new CombatSheet
            {
                CombatLevel = level, MaxHealth = CombatMath.PlayerMaxHealth(level, kit.ArmorHealth), DefenseLevel = level, Armor = kit.ArmorPower, IsPlayer = true,
                CritChance = CombatMath.BaseCritChance, CritDamage = CombatMath.BaseCritDamage,
            };
            sheet.Melee.DamageLevel = level; sheet.Melee.AimLevel = level; sheet.Melee.Power = kit.WeaponPower; sheet.Melee.Accuracy = kit.WeaponAim;
            sheet.Ranged.DamageLevel = level; sheet.Ranged.AimLevel = level; sheet.Ranged.Power = kit.RangedPower; sheet.Ranged.Accuracy = kit.RangedAim;
            sheet.Magic.DamageLevel = level; sheet.Magic.AimLevel = level; sheet.Magic.Power = kit.MagicPower; sheet.Magic.Accuracy = kit.MagicAim;
            kit.ApplyExtras(sheet);
            SetStats(line.Stats, tier).ApplyExtras(sheet, null);
            return sheet;
        }
    }
}

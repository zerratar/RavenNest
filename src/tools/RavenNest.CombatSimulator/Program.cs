using RavenNest.Combat;
using System;
using System.IO;
using System.Linq;

namespace RavenNest.CombatSimulator
{
    /// <summary>
    ///     The combat simulator (the game's docs/combat-v2-design.md, section 8).
    ///
    ///     parity [blows]   today's melee against the new rules for the same fighters at the same levels, both in
    ///                      their gear: hit rate, damage per swing and swings to kill an equal opponent.
    ///   enemies [fights]  a player at each fighting area's requirement against its enemies (data/enemies.json,
    ///                      exported from the game): swings and time to win, health left, fights lost.
    ///   elements [fights] the same player and enemies, the weapon's hits a quarter physical, fire, ice, lightning
    ///                      or poison: what each element does against each enemy's resistances.
    ///   bosses [fights]   raids and dungeons: groups of 5 to 50 players of a level against the boss the game makes
    ///                      for them, minutes to beat it and player falls.
    /// </summary>
    public static class Program
    {
        private static readonly int[] Levels = { 1, 10, 25, 50, 75, 100, 150, 200, 300, 400, 500, 700, 850, 999 };

        public static int Main(string[] args)
        {
            var command = args.Length > 0 ? args[0].ToLowerInvariant() : "parity";
            var gear = new GearPicker(Path.Combine(AppContext.BaseDirectory, "data", "items.json"));
            switch (command)
            {
                case "parity":
                    Parity(gear, args.Length > 1 && int.TryParse(args[1], out var n) ? n : 20000);
                    return 0;
                case "enemies":
                    EnemyScenarios.Run(gear, Path.Combine(AppContext.BaseDirectory, "data", "enemies.json"), args.Length > 1 && int.TryParse(args[1], out var f) ? f : 2000);
                    return 0;
                case "elements":
                    ElementScenarios.Run(gear, Path.Combine(AppContext.BaseDirectory, "data", "enemies.json"), args.Length > 1 && int.TryParse(args[1], out var ef) ? ef : 2000);
                    return 0;
                case "enemygear":
                    EnemyGearScenarios.Run(gear, Path.Combine(AppContext.BaseDirectory, "data", "enemies.json"),
                        args.Length > 1 && int.TryParse(args[1], out var gf) ? gf : 1000,
                        args.Length > 2 && double.TryParse(args[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var gs) ? gs : 1.0,
                        args.Length > 3 && double.TryParse(args[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var go) ? go : 1.0);
                    return 0;
                case "bosses":
                    BossScenarios.Run(gear, args.Length > 1 && int.TryParse(args[1], out var bf) ? bf : 200, args.Contains("tuned"));
                    return 0;
                default:
                    Console.WriteLine("Commands: parity [blows], enemies [fights], elements [fights], bosses [fights], enemygear [fights] [share]");
                    return 1;
            }
        }

        private static void Parity(GearPicker gear, int blows)
        {
            Console.WriteLine($"Melee, a player against an equal fighter in the same gear; {blows:N0} blows per level, {gear.Count} items.");
            Console.WriteLine("Today: HP = Health level. New: HP = 10 x Health level, blows x10, new hit chance and armour.");
            Console.WriteLine();
            Console.WriteLine($"{"Level",5} | {"Power",5} {"Aim",5} {"Armor",5} | {"Today hit",9} {"dmg",7} {"swings",7} | {"New hit",8} {"dmg",8} {"swings",7} | {"New/today",9}");
            Console.WriteLine(new string('-', 98));

            var random = new Random(1);
            var combatRandom = new SeededCombatRandom(1);
            foreach (var level in Levels)
            {
                var kit = gear.Melee(level);
                var today = new TodaysCombat.Fighter
                {
                    Attack = level, Strength = level, Defense = level, Health = level,
                    WeaponAim = kit.WeaponAim, WeaponPower = kit.WeaponPower, ArmorPower = kit.ArmorPower,
                };
                var todayEnemy = new TodaysCombat.Fighter
                {
                    Attack = level, Strength = level, Defense = level, Health = level,
                    WeaponAim = kit.WeaponAim, WeaponPower = kit.WeaponPower, ArmorPower = kit.ArmorPower, IsEnemy = true,
                };

                var todayHits = 0;
                var todayDamage = 0.0;
                for (var i = 0; i < blows; i++)
                {
                    var blow = TodaysCombat.MeleeBlow(today, todayEnemy, random);
                    if (blow > 0) todayHits++;
                    todayDamage += blow;
                }

                var sheet = Sheet(level, kit);
                var enemy = Sheet(level, kit);
                var newHits = 0;
                var newDamage = 0.0;
                for (var i = 0; i < blows; i++)
                {
                    var result = CombatMath.Attack(sheet, AttackStyle.Melee, enemy, combatRandom);
                    if (result.Hit) newHits++;
                    newDamage += result.Total;
                }

                var todayPerSwing = todayDamage / blows;
                var newPerSwing = newDamage / blows;
                var todaySwings = todayPerSwing > 0 ? level / todayPerSwing : double.PositiveInfinity;
                var newSwings = newPerSwing > 0 ? enemy.MaxHealth / newPerSwing : double.PositiveInfinity;
                Console.WriteLine($"{level,5} | {kit.WeaponPower,5} {kit.WeaponAim,5} {kit.ArmorPower,5} | {todayHits * 100.0 / blows,8:0.0}% {todayPerSwing,7:0.00} {todaySwings,7:0.0} | {newHits * 100.0 / blows,7:0.0}% {newPerSwing,8:0.0} {newSwings,7:0.0} | {newSwings / todaySwings,8:0.00}x");
            }
        }

        /// <summary>A fighter at <paramref name="level"/> in every combat skill, in <paramref name="kit"/>, as the new rules see it.</summary>
        private static CombatSheet Sheet(int level, Loadout kit)
        {
            var sheet = new CombatSheet
            {
                CombatLevel = level,
                MaxHealth = CombatMath.MaxHealth(level),
                DefenseLevel = level,
                Armor = kit.ArmorPower,
                CritChance = 0, // players have no crit chance today; kept out so the comparison is fair
            };
            sheet.Melee.DamageLevel = level;
            sheet.Melee.AimLevel = level;
            sheet.Melee.Power = kit.WeaponPower;
            sheet.Melee.Accuracy = kit.WeaponAim;
            return sheet;
        }
    }
}

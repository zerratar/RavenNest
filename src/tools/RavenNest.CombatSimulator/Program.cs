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
                case "pets":
                    PetScenarios.Run(gear,
                        args.Length > 1 && int.TryParse(args[1], out var pf) ? pf : 200,
                        args.Length > 2 && double.TryParse(args[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var pa) ? pa : 0.75,
                        args.Length > 3 && double.TryParse(args[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var po) ? po : 0.5);
                    return 0;
                case "builds":
                    BuildScenarios.Run(gear, args.Length > 1 && int.TryParse(args[1], out var bb) ? bb : 200);
                    return 0;
                case "treejson":
                    // the trees as the website's map takes them (js/rf-skill-tree.js), for a test page
                    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(SkillTrees.All.Select(n => new
                    {
                        id = n.Id, name = n.Name, tree = (int)n.Tree, tier = n.Tier,
                        kind = n.IsActive ? "active" : (n.ExtraLoadoutSlot || n.LastStand) ? "special" : "passive",
                        max = n.MaxRank, cost = n.Cost,
                    })));
                    return 0;
                case "bosses":
                    BossScenarios.Run(gear, args.Length > 1 && int.TryParse(args[1], out var bf) ? bf : 200, args.Contains("tuned"));
                    return 0;
                case "gearcaps":
                    foreach (var lv in new[] { 1, 5, 10, 11, 15, 20, 22, 25, 30, 33, 40, 44, 50, 55, 60, 66, 70, 77, 80, 88, 90, 99, 100, 110, 120, 150, 200, 300, 500, 999 })
                    {
                        var m = gear.Melee(lv); var r = gear.Ranged(lv); var g = gear.Magic(lv);
                        Console.WriteLine($"{lv} melee {m.WeaponPower}/{m.WeaponAim}/{m.ArmorPower} ranged {r.RangedPower}/{r.RangedAim}/{r.ArmorPower} magic {g.MagicPower}/{g.MagicAim}/{g.ArmorPower}");
                    }
                    return 0;
                case "paths":
                    // the capstone paths: each tree's three endings for the tree's own job; "paths 200 might" runs one tree ("all": every tree),
                    // and "paths 200 might aftershock=AFTERSHOCK:80%" tries other numbers for an ending
                    PathScenarios.Run(gear, args.Length > 1 && int.TryParse(args[1], out var pr) ? pr : 100, args.Length > 2 ? args[2] : null, args.Skip(3).ToArray());
                    return 0;
                case "tower":
                    // "tower": scaling by pressure (groups of 2, 5, 10 and 30, veterans); "tower 200 fixed": the fixed numbers for any group;
                    // "tower 200 norecover": knocked out players stay out of the attempt, as before TowerRecovery
                    if (args.Contains("norecover")) TowerScenarios.Recovery = false;
                    // "FURY=1.1,1.2,1.3", "SLAM=1.2,1.4", "FBP=1.5": other guardian numbers to try (TowerScenarios)
                    {
                        var inv2 = System.Globalization.CultureInfo.InvariantCulture;
                        double[] Numbers(string key)
                        {
                            var a = args.FirstOrDefault(x => x.StartsWith(key + "="));
                            return a == null ? null : a.Substring(key.Length + 1).Split(',').Select(x => double.Parse(x, inv2)).ToArray();
                        }
                        TowerScenarios.TryFury = Numbers("FURY");
                        TowerScenarios.TrySlam = Numbers("SLAM");
                        var pool = Numbers("FBP");
                        if (pool != null && pool.Length > 0) TowerScenarios.TryFloorBossPool = pool[0];
                        var fbf = Numbers("FBF");
                        if (fbf != null && fbf.Length > 0) TowerScenarios.TryFloorBossFury = fbf[0];
                    }
                    // "REC=60:0.5": other recovery numbers to try (seconds, health back)
                    var rec = args.FirstOrDefault(a => a.StartsWith("REC="));
                    if (rec != null)
                    {
                        var inv = System.Globalization.CultureInfo.InvariantCulture;
                        var parts = rec.Substring(4).Split(':');
                        if (parts[0].Length > 0) TowerScenarios.TrySeconds = double.Parse(parts[0], inv);
                        if (parts.Length > 1 && parts[1].Length > 0) TowerScenarios.TryHealth = double.Parse(parts[1], inv);
                    }
                    if (args.Contains("fixed")) TowerScenarios.Run(gear, args.Length > 1 && int.TryParse(args[1], out var tf) ? tf : 200);
                    else TowerScenarios.RunPressure(gear, args.Length > 1 && int.TryParse(args[1], out var tp) ? tp : 200);
                    return 0;
                case "taminggear":
                    // the Beastmaster line against each style's damage line, with a damage pet and without (the game's
                    // docs/taming-gear-design.md); "taminggear 200 PET_POWER=3:40" tries other numbers
                    TamingGearScenarios.Run(gear, args.Length > 1 && int.TryParse(args[1], out var tg) ? tg : 200, args.Skip(2).ToArray());
                    return 0;
                case "legendary":
                    // the Legendaries against the best normal item of their type, rank 1 and rank 5 (docs/legendary-design.md)
                    LegendaryScenarios.Run(gear, args.Length > 1 && int.TryParse(args[1], out var lr) ? lr : 200, args.Skip(2).ToArray());
                    return 0;
                default:
                    Console.WriteLine("Commands: parity [blows], enemies [fights], elements [fights], bosses [fights], enemygear [fights] [share], builds [runs], paths [runs] [tree], pets [runs] [armour share] [offence share], tower [attempts] [fixed]");
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
                MaxHealth = CombatMath.PlayerMaxHealth(level, kit.ArmorHealth),
                DefenseLevel = level,
                Armor = kit.ArmorPower,
                IsPlayer = true,
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

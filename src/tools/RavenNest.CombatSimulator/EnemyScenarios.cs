using RavenNest.Combat;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace RavenNest.CombatSimulator
{
    /// <summary>
    ///     A player against each island enemy (data/enemies.json, exported from the game's Ravenfall > Combat menu): a
    ///     player at the fighting area's level requirement, every combat skill at that level (Health at least 10), in the best melee gear for
    ///     it, fights one enemy at a time until one of them falls. Under today's rules and the new ones.
    /// </summary>
    public static class EnemyScenarios
    {
        /// <summary>The player's swing time (PlayerController.attackAnimationTime).</summary>
        public const double PlayerSwing = 1.5;

        private const double TimeLimit = 600;

        public static List<EnemyDefinition> Load(string path)
        {
            var options = new JsonSerializerOptions { IncludeFields = true, PropertyNameCaseInsensitive = true };
            options.Converters.Add(new JsonStringEnumConverter());
            return JsonSerializer.Deserialize<List<EnemyDefinition>>(File.ReadAllText(path), options);
        }

        /// <summary>The fighting area's level requirement from the definition's area ("Atria, Fighting Lv. 500").</summary>
        public static int Requirement(EnemyDefinition d)
        {
            var match = Regex.Match(d.Area ?? "", @"Lv\. (\d+)");
            return match.Success ? Math.Max(1, int.Parse(match.Groups[1].Value)) : Math.Max(1, d.CombatLevel);
        }

        private struct Outcome
        {
            public bool Won;
            public double Time;
            public int Swings;
            public double HealthLeft; // share of the player's pool
        }

        public static void Run(GearPicker gear, string path, int fights)
        {
            var definitions = Load(path).Where(d => !d.Scaled).OrderBy(Requirement).ThenBy(d => d.CombatLevel).ToList();
            Console.WriteLine($"{definitions.Count} enemies from {Path.GetFileName(path)}; a player at the area's requirement in its best melee gear, {fights:N0} fights each.");
            Console.WriteLine("Swings: the player's swings to win. HP left: the player's health after a win. Lost: fights the player lost.");
            Console.WriteLine();
            Console.WriteLine($"{"Enemy",-28} {"Req",4} {"Cb",4} | {"Today swings",12} {"time",6} {"HP left",7} {"lost",6} | {"New swings",10} {"time",6} {"HP left",7} {"lost",6}");
            Console.WriteLine(new string('-', 112));

            var random = new Random(7);
            var combatRandom = new SeededCombatRandom(7);
            foreach (var d in definitions)
            {
                var level = Requirement(d);
                var kit = gear.Melee(level);
                var today = Summarise(Enumerable.Range(0, fights).Select(_ => FightToday(level, kit, d, random)));
                var now = Summarise(Enumerable.Range(0, fights).Select(_ => FightNew(level, kit, d, combatRandom)));
                Console.WriteLine($"{Trim(d.Id, 28),-28} {level,4} {d.CombatLevel,4} | {today}  | {now}");
            }
        }

        private static string Trim(string s, int n) => s.Length <= n ? s : s.Substring(0, n);

        private static string Summarise(IEnumerable<Outcome> outcomes)
        {
            var list = outcomes.ToList();
            var won = list.Where(o => o.Won).ToList();
            var lost = 100.0 * (list.Count - won.Count) / list.Count;
            if (won.Count == 0) return $"{"-",12} {"-",6} {"-",7} {lost,5:0}%";
            return $"{won.Average(o => o.Swings),12:0.0} {won.Average(o => o.Time),5:0}s {won.Average(o => o.HealthLeft) * 100,6:0}% {lost,5:0}%";
        }

        /// <summary>Plays a fight on a timeline: the player swings every 1.5 s from the start, the enemy every interval.</summary>
        private static Outcome Fight(double playerHealth, double enemyHealth, double enemyInterval,
            Func<double> playerBlow, Func<double> enemyBlow)
        {
            var playerMax = playerHealth;
            var nextPlayer = 0.0;
            var nextEnemy = enemyInterval;
            var swings = 0;
            var time = 0.0;
            while (time < TimeLimit)
            {
                if (nextPlayer <= nextEnemy)
                {
                    time = nextPlayer;
                    swings++;
                    enemyHealth -= playerBlow();
                    if (enemyHealth <= 0) return new Outcome { Won = true, Time = time, Swings = swings, HealthLeft = playerHealth / playerMax };
                    nextPlayer += PlayerSwing;
                }
                else
                {
                    time = nextEnemy;
                    playerHealth -= enemyBlow();
                    if (playerHealth <= 0) return new Outcome { Won = false, Time = time, Swings = swings };
                    nextEnemy += enemyInterval;
                }
            }
            return new Outcome { Won = false, Time = time, Swings = swings };
        }

        private static Outcome FightToday(int level, Loadout kit, EnemyDefinition d, Random random)
        {
            var player = new TodaysCombat.Fighter
            {
                Attack = level, Strength = level, Defense = level, Health = HealthLevel(level),
                WeaponAim = kit.WeaponAim, WeaponPower = kit.WeaponPower, ArmorPower = kit.ArmorPower,
            };
            var enemy = new TodaysCombat.Fighter
            {
                Attack = d.Attack, Strength = d.Strength, Defense = d.Defense, Health = d.Health,
                WeaponAim = d.Accuracy, WeaponPower = d.Power, ArmorPower = d.Armor, IsEnemy = true,
            };
            return Fight(HealthLevel(level), d.Health, Interval(d),
                () => TodaysCombat.MeleeBlow(player, enemy, random),
                () => TodaysCombat.MeleeBlow(enemy, player, random));
        }

        private static Outcome FightNew(int level, Loadout kit, EnemyDefinition d, ICombatRandom random)
        {
            var player = new CombatSheet
            {
                CombatLevel = level,
                MaxHealth = CombatMath.MaxHealth(HealthLevel(level)),
                DefenseLevel = level,
                Armor = kit.ArmorPower,
                IsPlayer = true,
            };
            player.Melee.DamageLevel = level;
            player.Melee.AimLevel = level;
            player.Melee.Power = kit.WeaponPower;
            player.Melee.Accuracy = kit.WeaponAim;
            var enemy = d.Sheet();
            return Fight(player.MaxHealth, enemy.MaxHealth, Interval(d),
                () => CombatMath.Attack(player, AttackStyle.Melee, enemy, random).Total,
                () => CombatMath.Attack(enemy, d.Style, player, random).Total);
        }

        /// <summary>Characters start with Health 10.</summary>
        private static int HealthLevel(int level) => Math.Max(10, level);

        private static double Interval(EnemyDefinition d) => d.AttackInterval > 0 ? d.AttackInterval : 1;
    }
}

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

        internal struct Outcome
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
        internal static Outcome Fight(double playerHealth, double enemyHealth, double enemyInterval,
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
                MaxHealth = CombatMath.PlayerMaxHealth(HealthLevel(level), kit.ArmorHealth),
                DefenseLevel = level,
                Armor = kit.ArmorPower,
                IsPlayer = true,
            };
            player.Melee.DamageLevel = level;
            player.Melee.AimLevel = level;
            player.Melee.Power = kit.WeaponPower;
            player.Melee.Accuracy = kit.WeaponAim;
            kit.ApplyExtras(player);
            var enemy = d.Sheet();
            return FightWithEffects(player, enemy, Interval(d), d.Style, random);
        }

        /// <summary>
        ///     The new rules' fight with status effects, as the game plays them (CombatEffects): element blows burn, poison,
        ///     chill, freeze and stun both ways, damage over time once a second (burn by fire resistance, poison by poison
        ///     resistance, bleed by armour), chilled fighters swing slower, frozen and stunned ones not at all. One enemy,
        ///     so lightning's jumps go nowhere.
        /// </summary>
        internal static Outcome FightWithEffects(CombatSheet player, CombatSheet enemy, double enemyInterval, AttackStyle enemyStyle, ICombatRandom random)
        {
            const double step = 0.05;
            var playerEffects = new StatusEffectSet();
            var enemyEffects = new StatusEffectSet { IsBoss = enemy.IsBoss };
            double playerHealth = player.MaxHealth, enemyHealth = enemy.MaxHealth;
            double playerTimer = 0, enemyTimer = enemyInterval, tick = 0, time = 0;
            var swings = 0;
            Outcome Won() => new Outcome { Won = true, Time = time, Swings = swings, HealthLeft = Math.Max(0, playerHealth) / player.MaxHealth };
            Outcome Lost() => new Outcome { Won = false, Time = time, Swings = swings };
            while (time < TimeLimit)
            {
                time += step;
                if (!playerEffects.CannotAct) playerTimer -= step * playerEffects.AttackSpeedFactor;
                if (!enemyEffects.CannotAct) enemyTimer -= step * enemyEffects.AttackSpeedFactor;
                if (playerTimer <= 0)
                {
                    playerTimer += PlayerSwing;
                    swings++;
                    var blow = CombatMath.Attack(player, AttackStyle.Melee, enemy, random);
                    enemyHealth -= blow.Total;
                    if (blow.Procs != null && blow.Procs.Count > 0) enemyEffects.ApplyProcs(blow.Procs, random);
                    if (enemyHealth <= 0) return Won();
                }
                if (enemyTimer <= 0)
                {
                    enemyTimer += enemyInterval;
                    var blow = CombatMath.Attack(enemy, enemyStyle, player, random);
                    playerHealth -= blow.Total;
                    if (blow.Procs != null && blow.Procs.Count > 0) playerEffects.ApplyProcs(blow.Procs, random);
                    if (playerHealth <= 0) return Lost();
                }
                tick += step;
                if (tick >= 1)
                {
                    tick -= 1;
                    playerHealth -= OverTime(playerEffects.Tick(1), player);
                    if (playerHealth <= 0) return Lost();
                    enemyHealth -= OverTime(enemyEffects.Tick(1), enemy);
                    if (enemyHealth <= 0) return Won();
                }
            }
            return Lost();
        }

        // damage over time as the game takes it (CombatEffects.Update)
        internal static double OverTime(ElementValues dot, CombatSheet sheet)
        {
            if (dot.Total <= 0) return 0;
            var damage = dot.Physical * (1 - CombatMath.ArmorReduction(sheet.Armor))
                + dot.Fire * CombatMath.ResistanceMultiplier(sheet.Resistance.Fire, sheet.IsPlayer)
                + dot.Poison * CombatMath.ResistanceMultiplier(sheet.Resistance.Poison, sheet.IsPlayer);
            return Math.Round(damage * sheet.DamageTakenMultiplier);
        }

        /// <summary>Characters start with Health 10.</summary>
        internal static int HealthLevel(int level) => Math.Max(10, level);

        internal static double Interval(EnemyDefinition d) => d.AttackInterval > 0 ? d.AttackInterval : 1;
    }
}

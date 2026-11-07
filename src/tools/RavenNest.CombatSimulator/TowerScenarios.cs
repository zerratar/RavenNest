using RavenNest.Combat;
using System;
using System.Linq;

namespace RavenNest.CombatSimulator
{
    /// <summary>
    ///     Tower bosses (the game's docs/tower-design.md): a group of players at a level, each in the best melee kit for
    ///     it, against a floor's boss as RavenNest.Combat.Tower makes it. Tower rules: a player who falls is out for the
    ///     attempt, and the attempt is lost after Tower.BossTimeLimitSeconds. The boss swings at one player (the first
    ///     standing: the one holding it) and every SlamEvery swings hits everyone at SlamShare of a hit.
    ///     Players swing every 1.5 s. Each cell: the share of attempts won, and the minutes a win took.
    /// </summary>
    public static class TowerScenarios
    {
        private static readonly int[] Groups = { 1, 3, 5, 8, 15 };
        private const double PlayerSwing = 1.5;

        public static void Run(GearPicker gear, int fights)
        {
            Console.WriteLine($"Tower bosses; {fights} attempts per cell. Each cell: attempts won / minutes per win.");
            foreach (var levelShare in new[] { 1.0, 1.5 })
            {
                Console.WriteLine();
                Console.WriteLine($"Players at {levelShare:P0} of the floor's level:");
                Console.WriteLine($"{"Floor",5} {"Lvl",5} {"Boss HP",9} | " + string.Join(" | ", Groups.Select(g => $"{g + " players",13}")));
                Console.WriteLine(new string('-', 24 + Groups.Length * 16));
                var random = new SeededCombatRandom(7);
                foreach (var floor in Tower.Floors)
                {
                    var boss = Tower.Boss(floor);
                    var sheet = boss.Sheet();
                    var level = (int)(floor.Level * levelShare);
                    var kit = gear.Melee(level);
                    var cells = Groups.Select(players =>
                    {
                        var results = Enumerable.Range(0, fights).Select(_ => Fight(level, kit, players, floor.Boss, sheet, boss.AttackInterval, random)).ToList();
                        var won = results.Count(r => r.Won) / (double)results.Count;
                        var wins = results.Where(r => r.Won).ToList();
                        var minutes = wins.Count > 0 ? wins.Average(r => r.Time) / 60 : 0;
                        return $"{won,5:P0} {(wins.Count > 0 ? minutes.ToString("0.0") + " m" : "  -  "),6}";
                    });
                    Console.WriteLine($"{floor.Number,5} {floor.Level,5} {sheet.MaxHealth,9:N0} | " + string.Join(" | ", cells.Select(c => $"{c,13}")));
                }
            }
        }

        private static CombatSheet Player(int level, Loadout kit)
        {
            var sheet = new CombatSheet
            {
                CombatLevel = level,
                MaxHealth = CombatMath.MaxHealth(EnemyScenarios.HealthLevel(level)),
                DefenseLevel = level,
                Armor = kit.ArmorPower,
                IsPlayer = true,
            };
            sheet.Melee.DamageLevel = level;
            sheet.Melee.AimLevel = level;
            sheet.Melee.Power = kit.WeaponPower;
            sheet.Melee.Accuracy = kit.WeaponAim;
            kit.ApplyExtras(sheet);
            return sheet;
        }

        private struct Result
        {
            public bool Won;
            public double Time;
        }

        private static Result Fight(int level, Loadout kit, int players, TowerBoss def, CombatSheet boss, double bossSwing, ICombatRandom random)
        {
            var player = Player(level, kit);
            var health = Enumerable.Repeat((double)player.MaxHealth, players).ToArray();
            var nextSwing = Enumerable.Range(0, players).Select(i => PlayerSwing * i / players).ToArray();
            var bossHealth = (double)boss.MaxHealth;
            var nextBoss = bossSwing;
            var swings = 0;
            var time = 0.0;

            while (time < Tower.BossTimeLimitSeconds)
            {
                var who = -1;
                var when = nextBoss;
                for (var i = 0; i < players; ++i)
                {
                    if (health[i] <= 0) continue;
                    if (nextSwing[i] < when) { when = nextSwing[i]; who = i; }
                }
                time = when;
                if (time >= Tower.BossTimeLimitSeconds) break;

                if (who >= 0)
                {
                    bossHealth -= CombatMath.Attack(player, AttackStyle.Melee, boss, random).Total;
                    if (bossHealth <= 0) return new Result { Won = true, Time = time };
                    nextSwing[who] = time + PlayerSwing;
                    continue;
                }

                swings++;
                if (def.SlamEvery > 0 && swings % def.SlamEvery == 0)
                {
                    for (var i = 0; i < players; ++i)
                        if (health[i] > 0) health[i] -= CombatMath.Attack(boss, AttackStyle.Melee, player, random).Total * def.SlamShare;
                }
                else
                {
                    var target = Array.FindIndex(health, h => h > 0);
                    if (target >= 0) health[target] -= CombatMath.Attack(boss, AttackStyle.Melee, player, random).Total;
                }
                if (health.All(h => h <= 0)) return new Result { Won = false, Time = time };
                nextBoss = time + bossSwing;
            }

            return new Result { Won = false, Time = time };
        }
    }
}

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
            foreach (var (levelShare, sync) in new[] { (1.0, false), (1.5, false), (3.0, false), (3.0, true), (10.0, true) })
            {
                Console.WriteLine();
                Console.WriteLine($"Players at {levelShare:P0} of the floor's level" + (sync ? $", level synced to {Tower.SyncMargin:P0} of it (gear of their own level):" : ":"));
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
                        var results = Enumerable.Range(0, fights).Select(_ => Fight(level, kit, players, floor, floor.Boss, sheet, boss.AttackInterval, random, sync ? Tower.SyncLevel(floor.Number) : 0)).ToList();
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
                MaxHealth = CombatMath.PlayerMaxHealth(EnemyScenarios.HealthLevel(level), kit.ArmorHealth),
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

        private static Result Fight(int level, Loadout kit, int players, TowerFloorDefinition floor, TowerBoss def, CombatSheet boss, double bossSwing, ICombatRandom random, int syncLevel = 0)
        {
            var player = Player(level, kit);
            if (syncLevel > 0) Tower.Sync(player, floor.Number);
            var health = Enumerable.Repeat((double)player.MaxHealth, players).ToArray();
            var nextSwing = Enumerable.Range(0, players).Select(i => PlayerSwing * i / players).ToArray();
            var bossHealth = (double)boss.MaxHealth;
            var nextBoss = bossSwing;
            var swings = 0;
            var time = 0.0;

            // the adds: the floor's first monster at its level, called at each share of the boss's health
            var addDef = Tower.Enemy(floor, floor.Enemies[0]);
            var add = addDef.Sheet();
            var adds = new System.Collections.Generic.List<(double Health, double Next)>();
            var calls = 0;
            var enraged = false;
            var enragedBoss = boss;

            while (time < Tower.BossTimeLimitSeconds)
            {
                if (!enraged && def.EnrageSeconds > 0 && time >= def.EnrageSeconds)
                {
                    enraged = true;
                    bossSwing *= def.EnrageInterval;
                    enragedBoss = boss.Copy();
                    enragedBoss.Melee.Power = (int)(boss.Melee.Power * def.EnragePower);
                    enragedBoss.Magic.Power = (int)(boss.Magic.Power * def.EnragePower);
                }

                // the next thing to happen: a player's swing, the boss's or an add's
                var who = -1;
                var when = nextBoss;
                for (var i = 0; i < players; ++i)
                {
                    if (health[i] <= 0) continue;
                    if (nextSwing[i] < when) { when = nextSwing[i]; who = i; }
                }
                var addWho = -1;
                for (var a = 0; a < adds.Count; ++a)
                {
                    if (adds[a].Health > 0 && adds[a].Next < when) { when = adds[a].Next; addWho = a; who = -1; }
                }
                time = when;
                if (time >= Tower.BossTimeLimitSeconds) break;

                if (addWho >= 0)
                {
                    var target = random.Next() < 0.5 ? Array.FindIndex(health, h => h > 0) : RandomStanding(health, random);
                    if (target >= 0) health[target] -= CombatMath.Attack(add, addDef.Style, player, random).Total;
                    adds[addWho] = (adds[addWho].Health, time + addDef.AttackInterval);
                    if (health.All(h => h <= 0)) return new Result { Won = false, Time = time };
                    continue;
                }

                if (who >= 0)
                {
                    // the adds first: the players turn on them as they come
                    var addIndex = adds.FindIndex(x => x.Health > 0);
                    if (addIndex >= 0)
                    {
                        var hit = CombatMath.Attack(player, AttackStyle.Melee, add, random).Total;
                        adds[addIndex] = (adds[addIndex].Health - hit, adds[addIndex].Next);
                    }
                    else
                    {
                        bossHealth -= CombatMath.Attack(player, AttackStyle.Melee, boss, random).Total;
                        if (bossHealth <= 0) return new Result { Won = true, Time = time };
                        while (def.AddsAt != null && calls < def.AddsAt.Length && bossHealth <= boss.MaxHealth * def.AddsAt[calls])
                        {
                            calls++;
                            for (var a = 0; a < def.AddsCount; ++a) adds.Add((add.MaxHealth, time + addDef.AttackInterval * (1 + a * 0.2)));
                        }
                    }
                    nextSwing[who] = time + PlayerSwing;
                    continue;
                }

                swings++;
                var striker = enraged ? enragedBoss : boss;
                if (def.SlamEvery > 0 && swings % def.SlamEvery == 0)
                {
                    for (var i = 0; i < players; ++i)
                        if (health[i] > 0) health[i] -= CombatMath.Attack(striker, def.Style, player, random).Total * def.SlamShare;
                }
                else if (def.SpitEvery > 0 && swings % def.SpitEvery == 0)
                {
                    // at someone who is not holding it
                    var tank = Array.FindIndex(health, h => h > 0);
                    var target = RandomStanding(health, random, tank);
                    if (target < 0) target = tank;
                    if (target >= 0) health[target] -= CombatMath.Attack(striker, def.Style, player, random).Total * def.SpitShare;
                }
                else
                {
                    var target = Array.FindIndex(health, h => h > 0);
                    if (target >= 0) health[target] -= CombatMath.Attack(striker, def.Style, player, random).Total;
                }
                if (health.All(h => h <= 0)) return new Result { Won = false, Time = time };
                nextBoss = time + bossSwing;
            }

            return new Result { Won = false, Time = time };
        }

        private static int RandomStanding(double[] health, ICombatRandom random, int except = -1)
        {
            var standing = Enumerable.Range(0, health.Length).Where(i => health[i] > 0 && i != except).ToList();
            return standing.Count == 0 ? -1 : standing[Math.Min(standing.Count - 1, (int)(random.Next() * standing.Count))];
        }
    }
}

using RavenNest.Combat;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RavenNest.CombatSimulator
{
    /// <summary>
    ///     Raids and dungeons under the new rules: a group of players of one level, each in the best melee gear for it,
    ///     against the boss the game makes for them.
    ///     Raid boss (RaidDifficultySystem): the players' average levels, Health level times 100, their average weapon
    ///     power, aim and armour; it swings every 3 s (RaidBoss.prefab) at one player until that player falls.
    ///     Dungeon boss (DungeonManager.AdjustBossStats, DungeonBossController): the players' levels times 0.75 times the
    ///     dungeon's boss scale, Defense halved, Health level times 100 times (players / 10, at least 1) times the
    ///     health scale times 1.15; gear a third of theirs times the scale, armour a quarter of that, power 15% more;
    ///     a swing every 2 s (DungeonBoss.prefab). Normal dungeons scale 1, Heroic ones 2.5.
    ///     Players swing every 1.5 s; a fallen player is back after 4 s and a short walk (10 s in all).
    /// </summary>
    public static class BossScenarios
    {
        private static readonly int[] Levels = { 20, 50, 100, 200, 300, 500, 700, 999 };
        private static readonly int[] Groups = { 5, 10, 25, 50 };
        private const double PlayerSwing = 1.5;
        private const double Downtime = 10;
        private const double TimeLimit = 3600;

        /// <summary>
        ///     The tuning knobs the game uses with the new combat (CombatRules): the raid boss's pool times
        ///     <see cref="RaidPool"/> times (expected raiders / 10, at least half); a dungeon boss's pool times its
        ///     difficulty's pool share, and its combat scale moved toward 1 by <see cref="DungeonCombatEase"/>.
        /// </summary>
        public static double RaidPool = 2.5, NormalPool = 3, HeroicPool = 2, DungeonCombatEase = 0.27;

        public static void Run(GearPicker gear, int fights, bool tuned)
        {
            Console.WriteLine($"Raids and dungeons, new rules{(tuned ? $", tuned (raid pool x{RaidPool} by group, Normal pool x{NormalPool}, Heroic pool x{HeroicPool}, combat scale eased {DungeonCombatEase:P0})" : ", as the game makes them today")}; {fights} fights per cell.");
            Console.WriteLine("Each cell: minutes to beat the boss / player falls.");
            Report("Raid boss", gear, fights, (level, kit, players) => RaidBoss(level, kit, tuned ? RaidPool * Math.Max(0.5, players / 10.0) : 1), 3.0);
            Report("Dungeon boss, Normal", gear, fights, (level, kit, players) => DungeonBoss(level, kit, players, 1.0, tuned ? NormalPool : 1, tuned), 2.0);
            Report("Dungeon boss, Heroic", gear, fights, (level, kit, players) => DungeonBoss(level, kit, players, 2.5, tuned ? HeroicPool : 1, tuned), 2.0);
        }

        /// <summary>A dungeon type's boss combat scale with the new combat: toward 1 by DungeonCombatEase (2.5 becomes about 1.4).</summary>
        public static double EasedScale(double scale) => 1 + (scale - 1) * DungeonCombatEase;

        private static void Report(string title, GearPicker gear, int fights, Func<int, Loadout, int, CombatSheet> boss, double bossSwing)
        {
            Console.WriteLine();
            Console.WriteLine(title + ":");
            Console.WriteLine($"{"Level",5} | " + string.Join(" | ", Groups.Select(g => $"{g + " players",15}")));
            Console.WriteLine(new string('-', 8 + Groups.Length * 18));
            var random = new SeededCombatRandom(21);
            foreach (var level in Levels)
            {
                var kit = gear.Melee(level);
                var cells = Groups.Select(players =>
                {
                    var results = Enumerable.Range(0, fights).Select(_ => Fight(level, kit, players, boss(level, kit, players), bossSwing, random)).ToList();
                    var minutes = results.Average(r => r.Time) / 60;
                    var falls = results.Average(r => r.Falls);
                    var unbeaten = results.Count(r => !r.Won);
                    return $"{minutes,6:0.0} m {falls,5:0.0}{(unbeaten > 0 ? "*" : " ")}";
                });
                Console.WriteLine($"{level,5} | " + string.Join(" | ", cells.Select(c => $"{c,15}")));
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

        private static CombatSheet RaidBoss(int level, Loadout kit, double pool)
        {
            var sheet = new CombatSheet
            {
                CombatLevel = level,
                MaxHealth = (int)(CombatMath.MaxHealth(EnemyScenarios.HealthLevel(level) * 100) * pool),
                DefenseLevel = level,
                Armor = kit.ArmorPower,
                IsBoss = true,
            };
            sheet.Melee.DamageLevel = level;
            sheet.Melee.AimLevel = level;
            sheet.Melee.Power = kit.WeaponPower;
            sheet.Melee.Accuracy = kit.WeaponAim;
            return sheet;
        }

        private static CombatSheet DungeonBoss(int level, Loadout kit, int players, double healthScaleOfType, double pool, bool tuned)
        {
            var scale = tuned ? EasedScale(healthScaleOfType) : healthScaleOfType;
            var stat = (int)(level * 0.75 * scale);
            var healthScale = 100 * (Math.Max(10, players) / 10.0) * healthScaleOfType;
            var sheet = new CombatSheet
            {
                CombatLevel = stat,
                MaxHealth = (int)(CombatMath.MaxHealth((int)(stat * healthScale * 1.15)) * pool),
                DefenseLevel = stat / 2,
                Armor = (int)(kit.ArmorPower * 0.33 * scale * 0.25),
                IsBoss = true,
            };
            sheet.Melee.DamageLevel = stat;
            sheet.Melee.AimLevel = stat;
            sheet.Melee.Power = (int)(kit.WeaponPower * 0.33 * scale * 1.15);
            sheet.Melee.Accuracy = (int)(kit.WeaponAim * 0.33 * scale);
            return sheet;
        }

        private struct Result
        {
            public bool Won;
            public double Time;
            public int Falls;
        }

        private static Result Fight(int level, Loadout kit, int players, CombatSheet boss, double bossSwing, ICombatRandom random)
        {
            var player = Player(level, kit);
            var health = Enumerable.Repeat((double)player.MaxHealth, players).ToArray();
            var backAt = new double[players];
            var nextSwing = Enumerable.Range(0, players).Select(i => PlayerSwing * i / players).ToArray(); // staggered
            var bossHealth = (double)boss.MaxHealth;
            var nextBoss = bossSwing;
            var target = 0;
            var falls = 0;
            var time = 0.0;

            while (time < TimeLimit)
            {
                // the next thing to happen: a player's swing, or the boss's
                var who = -1;
                var when = nextBoss;
                for (var i = 0; i < players; ++i)
                {
                    var t = Math.Max(nextSwing[i], backAt[i]);
                    if (t < when) { when = t; who = i; }
                }
                time = when;

                if (who >= 0)
                {
                    if (health[who] <= 0) health[who] = player.MaxHealth; // back from the fall
                    bossHealth -= CombatMath.Attack(player, AttackStyle.Melee, boss, random).Total;
                    if (bossHealth <= 0) return new Result { Won = true, Time = time, Falls = falls };
                    nextSwing[who] = time + PlayerSwing;
                    continue;
                }

                // the boss: its target, or the next player standing
                if (health[target] <= 0 || backAt[target] > time)
                {
                    var standing = Enumerable.Range(0, players).Where(i => health[i] > 0 && backAt[i] <= time).ToList();
                    if (standing.Count > 0) target = standing[0];
                }
                if (health[target] > 0 && backAt[target] <= time)
                {
                    health[target] -= CombatMath.Attack(boss, AttackStyle.Melee, player, random).Total;
                    if (health[target] <= 0)
                    {
                        falls++;
                        backAt[target] = time + Downtime;
                    }
                }
                nextBoss = time + bossSwing;
            }

            return new Result { Won = false, Time = time, Falls = falls };
        }
    }
}

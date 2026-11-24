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
    ///     "tower" runs the scaling by pressure (RavenNest.Combat.TowerPressure, docs/tower-worlds-proposal.md section
    ///     12): groups of 2, 5, 10 and 30 fresh synced players, and veterans (a skill tree's passives), against typical
    ///     parts and their guardians, the monsters' health, the guardian's health, helpers and blows all by the fight's
    ///     pressure. "tower fixed" runs the old table: the fixed numbers for any group.
    /// </summary>
    public static class TowerScenarios
    {
        private static readonly int[] Groups = { 1, 3, 5, 8, 15 };
        private const double PlayerSwing = 1.5;

        // ---- Scaling by pressure ----------------------------------------------------------------------------------

        private static readonly int[] PressureGroups = { 2, 5, 10, 30 };

        /// <summary>
        ///     Seconds a player spends between kills: walking to the next monster, turning to it, blows wasted on one
        ///     that others are already finishing. Set so that five players make the 12 kills a minute measured in the
        ///     game on floor 1, part 6 (2026-11-24); the simulator's own fights would say 20.
        /// </summary>
        private const double WalkSeconds = 14;

        /// <summary>Monster spots on a part (the layouts have about this many).</summary>
        private const int Spots = 24;

        /// <summary>A veteran: every point of a tree's passives a level 999 character has (the Might tree's, a damage dealer's).</summary>
        private const string VeteranBuild = "brute-force:5;rend:5;executioner:5;titan:1;keen-eye:5;sharp-edge:5;deadly:5;swift:5";

        public static void RunPressure(GearPicker gear, int fights)
        {
            Console.WriteLine($"Tower, scaling by pressure; {fights} runs per cell. Players at each part's sync level in the best gear for it.");
            Console.WriteLine($"Pressure = players / {Tower.SuggestedGroup} x the group's strength ({TowerPressure.MinStrength:0.0} to {TowerPressure.MaxStrength:0.0}).");
            Console.WriteLine("Floor  bite  mercy: " + string.Join("   ", Enumerable.Range(1, 4).Select(f => $"{f}: {TowerPressure.Bite(f):P0} {TowerPressure.Mercy(f):P0}")));
            Console.WriteLine("A kill is worth (TowerPace), by players on the part: " + string.Join("   ", Enumerable.Range(1, 4).Select(f => $"floor {f}: " + string.Join(" ", new[] { 1, 2, 5, 10, 30 }.Select(g => $"{g}:{TowerPace.KillWeight(f, g).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)}")))));
            Console.WriteLine("A gathering round is worth, by gatherers: " + string.Join(" ", new[] { 1, 2, 4, 8, 12 }.Select(g => $"{g}:{TowerPace.GatherWeight(g).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)}")));
            var random = new SeededCombatRandom(11);
            var veteran = SkillBuild.Parse(VeteranBuild);

            foreach (var vet in new[] { false, true })
            {
                Console.WriteLine();
                Console.WriteLine(vet ? "Veterans (a tree's passives on top of the synced kit):" : "Fresh synced players:");
                Console.WriteLine($"{"Part",-6} {"Guardian",-30} | " + string.Join(" | ", PressureGroups.Select(g => $"{g + " players",-33}")));
                Console.WriteLine($"{"",-6} {"",-30} | " + string.Join(" | ", PressureGroups.Select(_ => $"{"press",5} {"part min",8} {"x five",6} {"boss HP",8} {"won",4} {"min",4}")));
                Console.WriteLine(new string('-', 38 + PressureGroups.Length * 43));
                var sums = new double[PressureGroups.Length];
                var raws = new double[PressureGroups.Length];
                var floorOf = 0;
                void Totals()
                {
                    if (floorOf == 0) return;
                    var five = Array.IndexOf(PressureGroups, Tower.SuggestedGroup);
                    Console.WriteLine($"{"",-6} {"floor " + floorOf + " in all",-30} | " + string.Join(" | ", PressureGroups.Select((g, i) =>
                        $"{"",5} {sums[i],8:0} {(five >= 0 ? sums[i] / sums[five] : 0),6:0.00} {"(kills as they are: " + (five >= 0 ? (raws[i] / raws[five]).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) : "-") + ")",-18}")));
                    Array.Clear(sums, 0, sums.Length);
                    Array.Clear(raws, 0, raws.Length);
                }
                foreach (var floor in Tower.Floors)
                {
                    if (floor.Floor != floorOf) Totals();
                    floorOf = floor.Floor;
                    var level = Tower.SyncLevel(floor.Number);
                    var kit = gear.Melee(level);
                    var player = Player(level, kit);
                    Tower.Sync(player, floor.Number, kit.ArmorHealth);
                    if (vet) veteran.Passives().ApplyExtras(player, null);
                    var strength = vet ? TowerPressure.Strength(player, AttackStyle.Melee, floor.Number) : 1.0;
                    var fiveMinutes = PartMinutes(floor, player, Tower.SuggestedGroup, TowerPressure.Of(Tower.SuggestedGroup, strength), new SeededCombatRandom(floor.Number), Math.Max(20, fights / 4), out _);
                    var column = 0;
                    var cells = PressureGroups.Select(players =>
                    {
                        var pressure = TowerPressure.Of(players, strength);
                        var minutes = PartMinutes(floor, player, players, pressure, new SeededCombatRandom(floor.Number), Math.Max(20, fights / 4), out var raw);
                        sums[column] += minutes;
                        raws[column] += raw;
                        column++;
                        // the guardian's own fight: against the group it is made for (a floor boss wants more)
                        var bossPressure = TowerPressure.Of(players, strength, Tower.BossGroup(floor));
                        var boss = Tower.Boss(floor, bossPressure);
                        var sheet = boss.Sheet();
                        var results = Enumerable.Range(0, fights).Select(_ => Fight(player, players, floor, floor.Boss, sheet, boss.AttackInterval, random, bossPressure)).ToList();
                        var wins = results.Where(r => r.Won).ToList();
                        return $"{pressure,5:0.00} {minutes,8:0} {minutes / fiveMinutes,6:0.00} {sheet.MaxHealth,8:N0} {wins.Count / (double)results.Count,4:P0} {(wins.Count > 0 ? (wins.Average(r => r.Time) / 60).ToString("0.0") : "-"),4}";
                    }).ToList();
                    Console.WriteLine($"{Tower.Short(floor.Number),-6} {(floor.IsFloorBoss ? "* " : "") + floor.Boss.Name,-30} | " + string.Join(" | ", cells) + (vet ? $"  strength {strength:0.00}" : ""));
                }
                Totals();
            }
            Console.WriteLine();
            Console.WriteLine("part min: minutes of fighting to finish the part's kill objectives, each kill worth what TowerPace says for the group (the other kinds run beside them). x five: against five players' minutes. * floor boss.");
        }

        /// <summary>
        ///     Minutes a group needs for a part's kill objectives: every player kills one monster after another (its
        ///     health by the pressure), walks to the next, and the part gives no more monsters than its spots bring back.
        ///     The part's kinds stand on the spots in turn, so each kind is its share of the kills. Each kill counts what
        ///     it is worth for the group (TowerPace.KillWeight); <paramref name="raw"/> is the minutes with every kill
        ///     counting one.
        /// </summary>
        private static double PartMinutes(TowerFloorDefinition floor, CombatSheet player, int players, double pressure, ICombatRandom random, int runs, out double raw)
        {
            var swing = PlayerSwing / CombatMath.Clamp(player.AttackSpeed, 0.2, CombatMath.MaxAttackSpeed);
            var perKind = new double[floor.Enemies.Length];
            var seconds = 0.0;
            for (var k = 0; k < floor.Enemies.Length; ++k)
            {
                var sheet = Tower.Enemy(floor, floor.Enemies[k], pressure).Sheet();
                var swings = 0.0;
                for (var r = 0; r < runs; ++r)
                {
                    var health = (double)sheet.MaxHealth;
                    while (health > 0) { health -= CombatMath.Attack(player, AttackStyle.Melee, sheet, random).Total; swings++; }
                }
                perKind[k] = swings / runs * swing + WalkSeconds;
                seconds += perKind[k];
            }
            // kills a minute: what the players can do, and what the spots give
            var cycle = seconds / floor.Enemies.Length;
            var rate = Math.Min(players * 60.0 / cycle, Spots * 60.0 / TowerPressure.RespawnSeconds(floor.Floor, pressure));
            var minutes = 0.0;
            foreach (var q in floor.Objectives)
            {
                if (q.Kind != TowerObjectiveKind.Kill) continue;
                minutes = Math.Max(minutes, q.Amount / (rate / floor.Enemies.Length));
            }
            raw = minutes;
            return minutes / TowerPace.KillWeight(floor.Floor, players);
        }

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
            return Fight(player, players, floor, def, boss, bossSwing, random, -1);
        }

        /// <param name="pressure">The fight's pressure for the helpers (their number and health); below 0: the fixed numbers.</param>
        private static Result Fight(CombatSheet player, int players, TowerFloorDefinition floor, TowerBoss def, CombatSheet boss, double bossSwing, ICombatRandom random, double pressure)
        {
            var playerSwing = PlayerSwing / CombatMath.Clamp(player.AttackSpeed, 0.2, CombatMath.MaxAttackSpeed);
            var health = Enumerable.Repeat((double)player.MaxHealth, players).ToArray();
            var nextSwing = Enumerable.Range(0, players).Select(i => playerSwing * i / players).ToArray();
            var bossHealth = (double)boss.MaxHealth;
            var nextBoss = bossSwing;
            var swings = 0;
            var time = 0.0;

            // the adds: the floor's first monster at its level, called at each share of the boss's health
            var addDef = pressure < 0 ? Tower.Enemy(floor, floor.Enemies[0]) : Tower.Enemy(floor, floor.Enemies[0], pressure, true);
            var add = addDef.Sheet();
            var addsCount = pressure < 0 ? def.AddsCount : TowerPressure.Helpers(def.AddsCount, floor.Floor, pressure);
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
                            for (var a = 0; a < addsCount; ++a) adds.Add((add.MaxHealth, time + addDef.AttackInterval * (1 + a * 0.2)));
                        }
                    }
                    nextSwing[who] = time + playerSwing;
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

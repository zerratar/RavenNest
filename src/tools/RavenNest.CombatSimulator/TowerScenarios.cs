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
        ///     Knocked out players recover and come back into the fight (TowerRecovery); "tower 200 norecover" fights
        ///     as before it, to compare: out for the rest of the attempt.
        /// </summary>
        public static bool Recovery = true;

        /// <summary>
        ///     Other numbers to try, "tower 200 REC=60:0.5": the seconds a knockout takes and the share of health on
        ///     the way back. Below 0: the rule's own (TowerRecovery).
        /// </summary>
        public static double TrySeconds = -1, TryHealth = -1;

        /// <summary>
        ///     Other guardian numbers to try, on top of the rule's own (TowerPressure.Fury, FloorBossPool):
        ///     "FURY=1.1,1.2,1.3" multiplies a guardian's blows by floor (the last number for every floor after),
        ///     "FBF=0.9" a floor boss's again, "SLAM=1.2,1.4,1.6" a slam's share, "FBP=1.5" a floor boss's health; a
        ///     group under five meets a share of each, as of the rule's. Null or below 0: nothing.
        /// </summary>
        public static double[] TryFury, TrySlam;
        public static double TryFloorBossPool = -1, TryFloorBossFury = -1;

        /// <summary>
        ///     Substitution (the game's docs/substitution-escape-idea.md): a player of the group (by index; 0 holds the
        ///     guardian) with the power escapes its slams and spits by its chance and cooldown, as the game rolls it; the
        ///     damage each player took, from area attacks and in all, and what the escapes saved, are counted. Null: nobody has it.
        /// </summary>
        public static SubstitutionPower[] Substitutions;
        public static double[] TakenArea, TakenAll, Saved;
        public static int[] Escaped, AreaBlows;

        // a blow on player i: counted; an area attack can be escaped
        private static void Hit(double[] health, int i, double damage, bool area, double time, double[] lastEscape, ICombatRandom random)
        {
            if (area && Substitutions != null && i < Substitutions.Length && Substitution.Escapes(Substitutions[i], lastEscape[i], time, random.Next()))
            {
                lastEscape[i] = time;
                if (Saved != null) { Saved[i] += damage; Escaped[i]++; AreaBlows[i]++; }
                return;
            }
            health[i] -= damage;
            if (TakenAll == null) return;
            TakenAll[i] += damage;
            if (area) { TakenArea[i] += damage; AreaBlows[i]++; }
        }

        /// <summary>
        ///     "substitution [fights]": five fresh players against each floor's guardians, the second (who does not hold the
        ///     guardian, so spits come for them too) with no Substitution, the attribute at 43% (a level 370 piece), and a
        ///     Chipped and a Perfect Shadowstone: the damage that player takes from slams and spits and in all, the blows
        ///     escaped, and the group's wins.
        /// </summary>
        public static void RunSubstitution(GearPicker gear, int fights)
        {
            var powers = new (string Name, SubstitutionPower Power)[]
            {
                ("none", default),
                ("attribute 43%", Substitution.FromAttribute(Substitution.AttributeChance(370))),
                ("Chipped Shadowstone", Substitution.FromGem(GemTier.Chipped)),
                ("Perfect Shadowstone", Substitution.FromGem(GemTier.Perfect)),
            };
            Console.WriteLine($"Substitution: five fresh players, player 2 with the power; {fights} fights a cell. Area: damage from slams and spits; all: every blow; escaped: area blows escaped of those that reached them.");
            foreach (var step in new[] { 5, 10, 15, 20, 25, 30 })
            {
                var floor = Tower.Get(step);
                if (floor == null || (floor.Boss.SlamEvery <= 0 && floor.Boss.SpitEvery <= 0)) continue;
                var (player, strength) = Players(floor, gear, false);
                var baseArea = 0.0;
                var baseAll = 0.0;
                foreach (var (name, power) in powers)
                {
                    Substitutions = new[] { default, power, default, default, default };
                    TakenArea = new double[5]; TakenAll = new double[5]; Saved = new double[5]; Escaped = new int[5]; AreaBlows = new int[5];
                    var (won, minutes) = Rate(floor, player, strength, 5, fights, step);
                    var area = TakenArea[1] / fights;
                    var all = TakenAll[1] / fights;
                    if (name == "none") { baseArea = area; baseAll = all; }
                    var escaped = AreaBlows[1] > 0 ? Escaped[1] / (double)AreaBlows[1] : 0;
                    Console.WriteLine($"{Tower.Short(step),6} {floor.Boss.Name,-34} {name,-20} area {area,9:N0} ({Less(area, baseArea),4}) all {all,9:N0} ({Less(all, baseAll),4}) escaped {escaped,4:P0} of {AreaBlows[1] / (double)fights,4:0.0} a fight | won {won,4:P0} in {minutes,4:0.0} min");
                }
            }
            Substitutions = null; TakenArea = TakenAll = Saved = null; Escaped = AreaBlows = null;
        }

        private static string Less(double v, double baseline) => baseline <= 0 ? "-" : (v >= baseline ? "+" : "-") + Math.Abs(1 - v / baseline).ToString("P0");

        // the players of a row: fresh synced ones in the best gear for the part, or veterans (a tree's passives on top)
        private static (CombatSheet Player, double Strength) Players(TowerFloorDefinition floor, GearPicker gear, bool vet)
        {
            var level = Tower.SyncLevel(floor.Number);
            var kit = gear.Melee(level);
            var player = Player(level, kit);
            Tower.Sync(player, floor.Number, kit.ArmorHealth);
            if (vet) SkillBuild.Parse(VeteranBuild).Passives().ApplyExtras(player, null);
            return (player, vet ? TowerPressure.Strength(player, AttackStyle.Melee, floor.Number) : 1.0);
        }

        private static (double Won, double Minutes) Rate(TowerFloorDefinition floor, CombatSheet player, double strength, int players, int fights, int seed)
        {
            var bossPressure = TowerPressure.Of(players, strength, Tower.BossGroup(floor));
            var boss = Tower.Boss(floor, bossPressure);
            var sheet = boss.Sheet();
            var random = new SeededCombatRandom(seed);
            var wins = 0;
            var time = 0.0;
            for (var i = 0; i < fights; ++i)
            {
                var result = Fight(player, players, floor, floor.Boss, sheet, boss.AttackInterval, random, bossPressure);
                if (!result.Won) continue;
                wins++;
                time += result.Time;
            }
            return (wins / (double)fights, wins > 0 ? time / wins / 60 : 0);
        }

        /// <summary>The wins of 100 a part's mini boss is aimed at for five fresh players: the floor's (90, 80, 70), five more on its first part and five fewer on its ninth.</summary>
        public static double Target(TowerFloorDefinition floor)
        {
            var aim = floor.Floor <= 1 ? 0.90 : floor.Floor == 2 ? 0.80 : 0.70;
            var part = Tower.PartOf(floor.Number);
            return aim + 0.05 - 0.10 * Math.Min(1, Math.Max(0, (part - 1) / 8.0));
        }

        /// <summary>
        ///     "tower why 23,25 300": for each part, what five and two fresh players win of its guardian as it is, and
        ///     with one thing taken away at a time (its slam, its spit, its helpers, its element, its own power, its
        ///     own attack speed, its own health): what makes it hard.
        /// </summary>
        public static void Why(GearPicker gear, int fights, int[] steps)
        {
            foreach (var step in steps)
            {
                var floor = Tower.Get(step);
                if (floor == null) continue;
                var b = floor.Boss;
                var (player, strength) = Players(floor, gear, false);
                string Row(string what)
                {
                    var five = Rate(floor, player, strength, 5, fights, step);
                    var two = Rate(floor, player, strength, 2, fights, step);
                    return $"  {what,-22} five {five.Won,5:P0} {five.Minutes,4:0.0} min   two {two.Won,5:P0} {two.Minutes,4:0.0} min";
                }
                Console.WriteLine($"{Tower.Short(step)} {b.Name}: power {b.PowerScale:0.00} every {b.AttackInterval:0.0}s, pool {b.HealthPool:0.00}, slam every {b.SlamEvery} x{b.SlamShare:0.0}, spit every {b.SpitEvery} x{b.SpitShare:0.0}, helpers {b.AddsCount} at {(b.AddsAt == null ? "-" : string.Join("/", b.AddsAt))}, element {b.Element} {b.ElementShare:0.0}, edge {b.Edge:0.00}; aim {Target(floor):P0}");
                Console.WriteLine(Row("as it is"));
                { var was = b.SlamEvery; b.SlamEvery = 0; Console.WriteLine(Row("without its slam")); b.SlamEvery = was; }
                if (b.SpitEvery > 0) { var was = b.SpitEvery; b.SpitEvery = 0; Console.WriteLine(Row("without its spit")); b.SpitEvery = was; }
                if (b.AddsCount > 0 && b.AddsAt != null) { var was = b.AddsCount; b.AddsCount = 0; Console.WriteLine(Row("without its helpers")); b.AddsCount = was; }
                if (b.Element != Element.Physical) { var was = b.ElementShare; var el = b.Element; b.Element = Element.Physical; b.ElementShare = 0; Console.WriteLine(Row("without its element")); b.Element = el; b.ElementShare = was; }
                if (Math.Abs(b.PowerScale - 1) > 0.001) { var was = b.PowerScale; b.PowerScale = 1; Console.WriteLine(Row("with a plain power")); b.PowerScale = was; }
                if (Math.Abs(b.AttackInterval - 2.5) > 0.001) { var was = b.AttackInterval; b.AttackInterval = 2.5; Console.WriteLine(Row("striking every 2.5 s")); b.AttackInterval = was; }
                if (Math.Abs(b.HealthPool - 1) > 0.001) { var was = b.HealthPool; b.HealthPool = 1; Console.WriteLine(Row("with a plain pool")); b.HealthPool = was; }
                if (b.Style != AttackStyle.Melee) { var was = b.Style; b.Style = AttackStyle.Melee; Console.WriteLine(Row("striking with a weapon")); b.Style = was; }
            }
        }

        /// <summary>
        ///     "tower tune 300": for every mini boss, the edge (TowerBoss.Edge) that brings five fresh players to its
        ///     part's aim (<see cref="Target"/>), found by halving, and what five and two fresh players win with it.
        /// </summary>
        public static void Tune(GearPicker gear, int fights)
        {
            Console.WriteLine($"{"Part",-6} {"Guardian",-30} {"aim",4} {"edge now",8} {"five now",8} {"edge",5} {"five",5} {"two",5}");
            foreach (var floor in Tower.Floors)
            {
                if (floor.IsFloorBoss) continue;
                var b = floor.Boss;
                var (player, strength) = Players(floor, gear, false);
                var aim = Target(floor);
                var was = b.Edge;
                var now = Rate(floor, player, strength, 5, fights, floor.Number).Won;
                double lo = 0.3, hi = 2.0;
                for (var i = 0; i < 9; ++i)
                {
                    b.Edge = (lo + hi) / 2;
                    if (Rate(floor, player, strength, 5, fights, floor.Number).Won > aim) lo = b.Edge; else hi = b.Edge;
                }
                b.Edge = Math.Round((lo + hi) / 2 / 0.05) * 0.05;
                var five = Rate(floor, player, strength, 5, fights, floor.Number + 1000).Won;
                var two = Rate(floor, player, strength, 2, fights, floor.Number + 1000).Won;
                Console.WriteLine($"{Tower.Short(floor.Number),-6} {b.Name,-30} {aim,4:P0} {was,8:0.00} {now,8:P0} {b.Edge,5:0.00} {five,5:P0} {two,5:P0}");
                b.Edge = was;
            }
        }

        private static double ByFloor(double[] values, int floor) =>
            values == null || values.Length == 0 ? 1 : values[Math.Min(values.Length, Math.Max(1, floor)) - 1];

        /// <summary>
        ///     Seconds a player spends between kills walking to the next monster. It was 14 when every spot held one
        ///     monster that stood still (five players made 12 kills a minute on floor 1, part 6, 2026-11-24); with packs
        ///     on every spot that wander and come for the players (2026-11-27) five make about 21 a minute on floors 1
        ///     and 2 and the walk is a small share of a kill (TowerPace.FightShare).
        /// </summary>
        private const double WalkSeconds = 2;

        /// <summary>A kill takes this much longer than one player's blows alone: turning to it, blows on one that others are finishing.</summary>
        private const double FightSlowdown = 1.2;

        /// <summary>Monsters on a part: about 24 spots with a pack of two or three on each (TowerFloor.PackSize).</summary>
        private const int Monsters = 60;

        /// <summary>The game keeps no more than this share of a part's monsters down, bringing them back sooner (5 s at least).</summary>
        private const double DownShare = 0.25, MinRespawnSeconds = 5;

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
                        // numbers to try, on top of the rule's own (TowerPressure.Fury, FloorBossPool)
                        boss.DamageScale *= 1 + (ByFloor(TryFury, floor.Floor) * (floor.IsFloorBoss && TryFloorBossFury > 0 ? TryFloorBossFury : 1) - 1) * Math.Min(1, pressure);
                        if (floor.IsFloorBoss && TryFloorBossPool > 0) boss.Health = Math.Max(1, (int)Math.Round(boss.Health * (1 + (TryFloorBossPool - 1) * Math.Min(1, pressure))));
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
        ///     health by the pressure), walks to the next, and the part gives no more monsters than come back. Players go
        ///     for the kind the part needs most (the game's TowerFloor), so the kill objectives fill together: their sum
        ///     over the kills a minute, a tenth more with a kind on the spots that no objective asks for. Each kill counts
        ///     what it is worth for the group (TowerPace.KillWeight); <paramref name="raw"/> is the minutes with every
        ///     kill counting one.
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
                perKind[k] = swings / runs * swing * FightSlowdown + WalkSeconds;
                seconds += perKind[k];
            }
            // kills a minute: what the players can do, and what the spots give
            var cycle = seconds / floor.Enemies.Length;
            var rate = Math.Min(players * 60.0 / cycle, Monsters * DownShare * 60.0 / MinRespawnSeconds);
            var kills = 0.0;
            var kinds = 0;
            foreach (var q in floor.Objectives)
            {
                if (q.Kind != TowerObjectiveKind.Kill) continue;
                kills += q.Amount;
                kinds++;
            }
            var minutes = kills / rate * (floor.Enemies.Length > kinds ? 1.1 : 1.0);
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
            // knocked out players recover by the entrance and come back (TowerRecovery): when each is back
            var backAt = new double[players];
            var down = new bool[players];
            // Substitution's last escape for each player (seconds; -1 never)
            var lastEscape = Enumerable.Repeat(-1.0, players).ToArray();

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

                // whoever has recovered is back, at a share of their health, after the walk to the guardian; whoever
                // just fell starts to recover (everyone down at the same moment was the loss, where it happened)
                for (var i = 0; i < players; ++i)
                {
                    if (health[i] > 0) continue;
                    if (!down[i])
                    {
                        down[i] = true;
                        backAt[i] = time + (TrySeconds > 0 ? TrySeconds : TowerRecovery.Seconds);
                    }
                    else if (Recovery && time >= backAt[i])
                    {
                        down[i] = false;
                        health[i] = player.MaxHealth * (TryHealth > 0 ? TryHealth : TowerRecovery.ReturnHealth);
                        nextSwing[i] = time + TowerRecovery.WalkBackSeconds;
                    }
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
                    if (target >= 0) Hit(health, target, CombatMath.Attack(add, addDef.Style, player, random).Total, false, time, lastEscape, random);
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
                        if (health[i] > 0) Hit(health, i, CombatMath.Attack(striker, def.Style, player, random).Total * def.SlamShare * ByFloor(TrySlam, floor.Floor), true, time, lastEscape, random);
                }
                else if (def.SpitEvery > 0 && swings % def.SpitEvery == 0)
                {
                    // at someone who is not holding it
                    var tank = Array.FindIndex(health, h => h > 0);
                    var target = RandomStanding(health, random, tank);
                    if (target < 0) target = tank;
                    if (target >= 0) Hit(health, target, CombatMath.Attack(striker, def.Style, player, random).Total * def.SpitShare, true, time, lastEscape, random);
                }
                else
                {
                    var target = Array.FindIndex(health, h => h > 0);
                    if (target >= 0) Hit(health, target, CombatMath.Attack(striker, def.Style, player, random).Total, false, time, lastEscape, random);
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

using RavenNest.Combat;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RavenNest.CombatSimulator
{
    /// <summary>
    ///     The capstone paths (the game's docs/capstone-paths-design.md): each combat tree's three endings compared for
    ///     the tree's own job. A level 999 player in the standard kit for its style has every node of the tree below
    ///     the capstones (so every ending's "asks for" is met) and its actives in the loadout; the same fight is run
    ///     with no ending and with each of the three, and the ending's worth is what it adds.
    ///
    ///     The fights, 300 s each:
    ///       one      enemies of the player's level one after another (island enemies: three quarters of the kit's
    ///                armor, half its power; the next one arrives 2 s after a kill)
    ///       pack     four of them at once (area skills and rules for three or more enemies fire)
    ///       boss     one boss whose pool lasts about 90 s for the player without an ending, then the next
    ///       hard     the pack hitting at the kit's full power (a dungeon room, a tower floor)
    ///     A player that falls is back after 10 s. Tanks fight beside an ally the enemies also go for (by threat);
    ///     healers keep a tank alive in front of the pack.
    ///
    ///     What it cannot model is said where the table is printed, not guessed: positions (everything is in reach),
    ///     pets, loot, gathering.
    /// </summary>
    public static class PathScenarios
    {
        private const int Level = 999;
        private const double Seconds = 300;
        private const double Swing = 1.5;
        private const double EnemySwing = 2.0;
        private const double Arrival = 2.0;
        private const double Downtime = 10;

        public sealed class Setup
        {
            public string Name;
            public AttackStyle Style = AttackStyle.Melee;
            public SkillBuild Build;
            public int Pack = 1;
            public bool Boss;
            public double Offence = 0.5;
            /// <summary>An ally beside the player whom the enemies also attack (Bulwark).</summary>
            public bool Ally;
            /// <summary>The player heals a tank in front of the pack instead of fighting (Restoration).</summary>
            public bool Healer;
            public bool Undead;
            public double BossPool;
            /// <summary>
            ///     The player's health is held at this share (0: it rises and falls with the fight, and the player can
            ///     fall). A fighter alone only loses health until it falls, which says nothing about how hurt players
            ///     are in a real fight (healers, food, regeneration between fights), so the damage tables name the
            ///     health they are for.
            /// </summary>
            public double Health;
            /// <summary>A healer of the player's level casts a basic heal on the player this often, in seconds (Vitality); 0: no healer.</summary>
            public double Healed;
            public double Seconds = 300;
        }

        public sealed class Outcome
        {
            public double Damage, Taken, Absorbed, SelfHealed, Falls, Kills, HealthShare;
            public double AllyTaken, AllyFalls, Healed, Shielded, Strike, Saves;
            public int Samples;

            public static Outcome Average(List<Outcome> runs) => new Outcome
            {
                Damage = runs.Average(r => r.Damage), Taken = runs.Average(r => r.Taken), Absorbed = runs.Average(r => r.Absorbed),
                SelfHealed = runs.Average(r => r.SelfHealed), Falls = runs.Average(r => r.Falls), Kills = runs.Average(r => r.Kills),
                HealthShare = runs.Average(r => r.Samples > 0 ? r.HealthShare / r.Samples : 1),
                AllyTaken = runs.Average(r => r.AllyTaken), AllyFalls = runs.Average(r => r.AllyFalls), Healed = runs.Average(r => r.Healed),
                Shielded = runs.Average(r => r.Shielded), Strike = runs.Average(r => r.Strike), Saves = runs.Average(r => r.Saves), Samples = 1,
            };
        }

        private sealed class Foe
        {
            public CombatSheet Sheet;
            public double Health;
            public readonly StatusEffectSet Fx = new StatusEffectSet();
            public double NextAttack, StunUntil, TakenUntil, TakenAmount, SoftUntil, SoftAmount, ArrivesAt;
            public bool Alive => Health > 0;
        }

        // ---- the builds: every node of a tree below its capstones, and its actives in the loadout ----

        private static string Full(SkillTree tree) => string.Join(";", SkillTrees.Of(tree).Where(n => !n.Capstone).Select(n => n.Id + ":" + n.MaxRank));

        private static SkillBuild Build(string nodes, string loadout, string capstone)
        {
            var text = nodes + (capstone != null ? ";" + capstone + ":1" : "") + "|" + loadout;
            var build = SkillBuild.Parse(text);
            if (capstone != null && !build.Has(capstone)) throw new InvalidOperationException(capstone + " is not in the build");
            return build;
        }

        private sealed class TreeCase
        {
            public SkillTree Tree;
            public AttackStyle Style = AttackStyle.Melee;
            public string Nodes, Loadout;
            public (string name, Action<Setup> shape)[] Fights;
            public Func<Outcome, Outcome, string>[] Cells;
            public string[] Columns;
        }

        private static readonly (string, Action<Setup>)[] DamageFights =
        {
            ("one", s => { s.Health = 0.9; s.Offence = 0.25; }),
            ("pack", s => { s.Pack = 4; s.Health = 0.9; s.Offence = 0.25; }),
            ("boss", s => { s.Boss = true; s.Health = 0.9; s.Offence = 1; }),
        };

        public static void Run(GearPicker allGear, int runs, string only, string[] overrides)
        {
            System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
            // "first-blood=FIRST_STRIKE:25%;POISON_ON_CRIT:6%" tries other numbers for an ending without a rebuild
            foreach (var o in overrides ?? Array.Empty<string>())
            {
                var eq = o.IndexOf('=');
                var node = eq > 0 ? SkillTrees.Get(o.Substring(0, eq)) : null;
                if (node == null) { Console.WriteLine("Unknown: " + o); continue; }
                node.Passive = o.Substring(eq + 1);
                Console.WriteLine("Trying " + node.Name + ": " + node.Passive);
            }
            if (only == "all") only = null;
            var gear = allGear.Standard();
            Console.WriteLine($"Capstone paths: a level {Level} player with every node of its tree below the capstones, {Seconds:0} s a fight, {runs} runs.");
            Console.WriteLine("Damage columns: damage dealt as a multiple of the same build with no ending, the player held at 90% health (hurt: at 50%). * marks the best of the three, and the others say how far behind it they are.");
            Console.WriteLine();

            string might = Full(SkillTree.Might), mightLoadout = "crushing-blow,leap,cleave,whirlwind";
            var cases = new List<TreeCase>
            {
                new TreeCase { Tree = SkillTree.Precision, Nodes = Full(SkillTree.Precision), Loadout = "flurry,expose-weakness,disarm" },
                new TreeCase { Tree = SkillTree.Might, Nodes = might, Loadout = mightLoadout,
                    Fights = DamageFights.Concat(new (string, Action<Setup>)[] { ("pack, hurt", s => { s.Pack = 4; s.Health = 0.5; s.Offence = 1; }), ("boss, hurt", s => { s.Boss = true; s.Health = 0.5; s.Offence = 1; }) }).ToArray() },
                new TreeCase { Tree = SkillTree.Marksman, Style = AttackStyle.Ranged, Nodes = Full(SkillTree.Marksman), Loadout = "rapid-fire,piercing-shot,arrow-rain,multishot" },
                new TreeCase { Tree = SkillTree.Elements, Style = AttackStyle.Magic, Nodes = Full(SkillTree.Elements), Loadout = "meteor,chain-lightning,fireball,toxic-cloud" },
                new TreeCase { Tree = SkillTree.Hunter, Nodes = might + ";" + Full(SkillTree.Hunter), Loadout = "mark-prey,execute,crushing-blow,cleave" },
            };

            foreach (var c in cases)
            {
                if (only != null && !c.Tree.ToString().StartsWith(only, StringComparison.OrdinalIgnoreCase)) continue;
                DamageTable(gear, c, runs);
            }
            if (only == null || "vitality".StartsWith(only, StringComparison.OrdinalIgnoreCase)) Vitality(gear, might, runs);
            if (only == null || "bulwark".StartsWith(only, StringComparison.OrdinalIgnoreCase)) Bulwark(gear, runs);
            if (only == null || "restoration".StartsWith(only, StringComparison.OrdinalIgnoreCase)) Restoration(gear, runs);
            if (only == null || "beastmaster".StartsWith(only, StringComparison.OrdinalIgnoreCase)) Beastmaster();
        }

        private static Outcome Fight(GearPicker gear, Setup setup, int runs, int seed)
        {
            var random = new SeededCombatRandom(seed);
            if (setup.Boss && setup.BossPool <= 0) throw new InvalidOperationException("a boss needs a pool");
            return Outcome.Average(Enumerable.Range(0, runs).Select(_ => One(gear, setup, random)).ToList());
        }

        /// <summary>A boss's pool: what the build without an ending deals in 90 s.</summary>
        private static double BossPool(GearPicker gear, Setup without, int runs)
        {
            var probe = new Setup { Name = "probe", Style = without.Style, Build = without.Build, Boss = true, BossPool = 1e12, Offence = without.Offence, Ally = without.Ally, Healer = without.Healer, Health = without.Health };
            return Fight(gear, probe, Math.Max(20, runs / 4), 7).Damage / Seconds * 90;
        }

        private static void DamageTable(GearPicker gear, TreeCase c, int runs)
        {
            var fights = c.Fights ?? DamageFights;
            var endings = SkillTrees.Capstones(c.Tree);
            Console.WriteLine($"{c.Tree} ({SkillTrees.SkillName(SkillTrees.SkillOf(c.Tree))}), {c.Style.ToString().ToLowerInvariant()}: damage dealt, x the build without an ending");
            Console.WriteLine($"{"Ending",-22} | " + string.Join(" | ", fights.Select(f => $"{f.name,14}")));
            Console.WriteLine(new string('-', 25 + fights.Length * 17));
            var rows = new List<(string name, double[] x, Outcome last)>();
            var pools = new double[fights.Length];
            var none = new Outcome[fights.Length];
            for (var f = 0; f < fights.Length; ++f)
            {
                var s = new Setup { Style = c.Style, Build = Build(c.Nodes, c.Loadout, null) };
                fights[f].shape(s);
                if (s.Boss) s.BossPool = pools[f] = BossPool(gear, s, runs);
                none[f] = Fight(gear, s, runs, 100 + f);
            }
            foreach (var ending in endings)
            {
                var x = new double[fights.Length];
                Outcome last = null;
                for (var f = 0; f < fights.Length; ++f)
                {
                    var s = new Setup { Style = c.Style, Build = Build(c.Nodes, c.Loadout, ending.Id), BossPool = pools[f] };
                    fights[f].shape(s);
                    last = Fight(gear, s, runs, 100 + f);
                    x[f] = last.Damage / Math.Max(1, none[f].Damage);
                }
                rows.Add((ending.Name + " (" + ending.Path + ")", x, last));
            }
            foreach (var (name, x, last) in rows)
            {
                var cells = Enumerable.Range(0, fights.Length).Select(f =>
                {
                    var best = rows.Max(r => r.x[f]);
                    return x[f] >= best - 1e-9 ? $"{x[f],7:0.000}x *    " : $"{x[f],7:0.000}x {(x[f] / best - 1) * 100,4:0}% ";
                });
                Console.WriteLine($"{Short(name),-22} | " + string.Join(" | ", cells.Select(cell => $"{cell,14}")));
            }
            Console.WriteLine($"{"(no ending)",-22} | " + string.Join(" | ", none.Select(n => $"{n.Damage / Seconds,9:0} dps  ")));
            Console.WriteLine();
        }

        private static string Short(string name) => name.Length <= 22 ? name : name.Substring(0, name.IndexOf(" (", StringComparison.Ordinal));

        // Vitality: staying alive, on a melee fighter with all of Might. What matters is how long a life lasts where
        // the enemies hit hard: alone, and with a healer behind.
        private static void Vitality(GearPicker gear, string might, int runs)
        {
            var nodes = might + ";" + Full(SkillTree.Vitality);
            const string loadout = "second-wind,iron-skin,crushing-blow,cleave,whirlwind";
            Console.WriteLine("Vitality (staying alive), a melee fighter with all of Might, 600 s. Alone: one enemy at a time hitting at full power. Healed: three at once at full power, and a healer of the same level who has two others to look after (a heal every 4.5 s).");
            Console.WriteLine($"{"Ending",-22} | {"alone: s a life",15} {"x",7} {"damage",8} | {"healed: s a life",16} {"x",7} {"damage",8}");
            Console.WriteLine(new string('-', 95));
            var names = new List<string> { null };
            names.AddRange(SkillTrees.Capstones(SkillTree.Vitality).Select(n => n.Id));
            Outcome[] baseline = null;
            double Life(Outcome r) => 600 / (r.Falls + 1);
            foreach (var id in names)
            {
                var results = new[]
                {
                    Fight(gear, new Setup { Build = Build(nodes, loadout, id), Pack = 1, Offence = 1, Seconds = 600 }, runs, 300),
                    Fight(gear, new Setup { Build = Build(nodes, loadout, id), Pack = 3, Offence = 1, Healed = 4.5, Seconds = 600 }, runs, 301),
                };
                baseline ??= results;
                var label = id == null ? "(no ending)" : Short(SkillTrees.Get(id).Name + " (" + SkillTrees.Get(id).Path + ")");
                Console.WriteLine($"{label,-22} | " + string.Join(" | ", results.Select((r, i) =>
                    Life(r).ToString("0.0").PadLeft(i == 0 ? 15 : 16) + $" {Life(r) / Life(baseline[i]),6:0.000}x {r.Damage / Math.Max(1, baseline[i].Damage),7:0.000}x")));
            }
            Console.WriteLine("s a life: the fight's 600 s over the lives it took (a fall costs 10 s). Longer is better.");
            Console.WriteLine();
        }

        // Bulwark: the tank beside an ally. Enemies pick the tank or the ally by threat (the tank's threat against an
        // ally's 1, both dealing about the same damage).
        private static void Bulwark(GearPicker gear, int runs)
        {
            var nodes = Full(SkillTree.Bulwark);
            const string loadout = "shield-bash,shield-wall,rally,taunt";
            Console.WriteLine("Bulwark (the tank beside one ally, the pack hitting at full power, the tank held at 70% health):");
            Console.WriteLine($"{"Ending",-22} | {"tank taken/min",14} {"ally taken/min",14} {"both/min",9} {"x",7} {"tank damage",11}");
            Console.WriteLine(new string('-', 90));
            var names = new List<string> { null };
            names.AddRange(SkillTrees.Capstones(SkillTree.Bulwark).Select(n => n.Id));
            Outcome baseline = null;
            foreach (var id in names)
            {
                var r = Fight(gear, new Setup { Build = Build(nodes, loadout, id), Pack = 4, Offence = 1, Ally = true, Health = 0.7 }, runs, 400);
                baseline ??= r;
                var label = id == null ? "(no ending)" : Short(SkillTrees.Get(id).Name + " (" + SkillTrees.Get(id).Path + ")");
                var tank = (r.Taken + r.Absorbed) / Seconds * 60;
                var ally = r.AllyTaken / Seconds * 60;
                var before = (baseline.Taken + baseline.Absorbed + baseline.AllyTaken) / Seconds * 60;
                Console.WriteLine($"{label,-22} | {tank,14:0} {ally,14:0} {tank + ally,9:0} {(tank + ally) / before,6:0.000}x {r.Damage / Math.Max(1, baseline.Damage),10:0.000}x");
            }
            Console.WriteLine("The ally is an untrained fighter in the same kit. Not modelled: Taunt (enemies are split by threat alone), more than one ally (Shield of the Many takes its share from every ally near, so it grows with the group; Unbreakable does not).");
            Console.WriteLine();
        }

        // Restoration: the healer behind a tank in front of the pack. The tank is always hurt, so every heal counts in
        // full and what is measured is what the healer puts out.
        private static void Restoration(GearPicker gear, int runs)
        {
            var nodes = Full(SkillTree.Restoration);
            const string loadout = "mend,renew,barrier,sanctuary";
            Console.WriteLine("Restoration (a healer behind one tank that is always hurt, in front of the pack): what the healer puts out a minute");
            Console.WriteLine($"{"Ending",-22} | {"healing",9} {"shields",9} {"both",9} {"x",7} | {"damage",9} {"of a fighter's",14} | {"saves",6} {"health saved",12}");
            Console.WriteLine(new string('-', 118));
            var names = new List<string> { null };
            names.AddRange(SkillTrees.Capstones(SkillTree.Restoration).Select(n => n.Id));
            var fighter = Fight(gear, new Setup { Build = Build(Full(SkillTree.Might), "crushing-blow,leap,cleave,whirlwind", null), Pack = 4, Offence = 0, Health = 0.9 }, Math.Max(20, runs / 4), 510);
            Outcome baseline = null;
            foreach (var id in names)
            {
                var r = Fight(gear, new Setup { Style = AttackStyle.Healing, Build = Build(nodes, loadout, id), Pack = 4, Offence = 1, Healer = true }, runs, 500);
                baseline ??= r;
                var label = id == null ? "(no ending)" : Short(SkillTrees.Get(id).Name + " (" + SkillTrees.Get(id).Path + ")");
                var angel = id != null ? GearStats.Parse(SkillTrees.Get(id).Passive)[GearStat.GuardianAngel] : 0;
                var saves = angel > 0 ? 60 / PathRules.AngelSeconds : 0;
                var saved = saves * PathRules.AngelHeal(Player(gear.Melee(Level), AttackStyle.Melee, null).MaxHealth, angel);
                double PerMinute(double v) => v / Seconds * 60;
                Console.WriteLine($"{label,-22} | {PerMinute(r.Healed),9:0} {PerMinute(r.Shielded),9:0} {PerMinute(r.Healed + r.Shielded),9:0} {(r.Healed + r.Shielded) / (baseline.Healed + baseline.Shielded),6:0.000}x | {PerMinute(r.Strike),9:0} {r.Strike / Math.Max(1, fighter.Damage) * 100,13:0}% | {saves,6:0.0} {saved,12:0}");
            }
            Console.WriteLine($"A fighter: a melee fighter with all of Might and no ending, {fighter.Damage / Seconds * 60:0} a minute on the same pack. Shields: every shield given is counted as used up.");
            Console.WriteLine("saves: at most, when somebody near would die that often; the save's worth is the fall it prevents (10 s out of the fight), which this table cannot price. Not modelled: more than one ally to heal, the undead (Holy Fire's bonus against them), Cleanse.");
            Console.WriteLine();
        }

        private static void Beastmaster()
        {
            Console.WriteLine("Beastmaster: not simulated here (the pets command fights one pet). A pet's blows and heals scale with its power, so:");
            var alpha = GearStats.Parse(SkillTrees.Get("alpha").Passive);
            Console.WriteLine($"  Pack Leader: the pet and a packmate at half strength: {1 + 0.5:0.00}x the pet's damage and healing, the pet's own health unchanged (the packmate is nobody's target and guards nobody).");
            Console.WriteLine($"  Alpha: {1 + alpha[GearStat.PetPower]:0.00}x the pet's damage and healing, {1 + alpha[GearStat.PetHealth]:0.00}x its health (a tank pet lasts that much longer).");
            Console.WriteLine("  Trailblazer: nothing in a fight; a gatherer pet's skills are " + SkillTexts.Share(GearStats.Parse(SkillTrees.Get("trailblazer").Passive)[GearStat.PetGathering]) + " stronger.");
            Console.WriteLine();
        }

        // ---- one fight ----

        private static Loadout Kit(GearPicker gear, AttackStyle style) =>
            style == AttackStyle.Ranged ? gear.Ranged(Level) : style == AttackStyle.Magic || style == AttackStyle.Healing ? gear.Magic(Level) : gear.Melee(Level);

        private static CombatSheet Player(Loadout kit, AttackStyle style, SkillBuild build)
        {
            var sheet = new CombatSheet { CombatLevel = Level, MaxHealth = CombatMath.PlayerMaxHealth(Level, kit.ArmorHealth), DefenseLevel = Level, Armor = kit.ArmorPower, IsPlayer = true };
            var s = sheet.Style(style);
            s.DamageLevel = Level;
            s.AimLevel = Level;
            s.Power = style == AttackStyle.Ranged ? kit.RangedPower : style == AttackStyle.Magic || style == AttackStyle.Healing ? kit.MagicPower : kit.WeaponPower;
            s.Accuracy = style == AttackStyle.Ranged ? kit.RangedAim : style == AttackStyle.Magic ? kit.MagicAim : kit.WeaponAim;
            kit.ApplyExtras(sheet);
            build?.Passives().ApplyExtras(sheet, null);
            return sheet;
        }

        private static Outcome One(GearPicker gear, Setup setup, ICombatRandom random)
        {
            var o = new Outcome();
            var Seconds = setup.Seconds;
            var kit = Kit(gear, setup.Style);
            var build = setup.Build;
            var player = Player(kit, setup.Style, build);
            var melee = gear.Melee(Level);
            var baseTaken = player.DamageTakenMultiplier;
            var state = new PathState();
            var style = setup.Style;

            // the ally: a tank the healer keeps alive, or the fighter beside the tank
            var ally = setup.Ally || setup.Healer ? Player(melee, AttackStyle.Melee, null) : null;
            var hots = new List<(double perSecond, double until)>();

            Foe Spawn(double at)
            {
                var sheet = new CombatSheet
                {
                    CombatLevel = Level, DefenseLevel = Level, IsBoss = setup.Boss,
                    Armor = (int)(melee.ArmorPower * (setup.Boss ? 1 : 0.75)),
                    MaxHealth = setup.Boss ? (int)Math.Min(int.MaxValue, setup.BossPool) : CombatMath.MaxHealth(Level),
                    Family = setup.Undead ? EnemyFamily.Undead : (EnemyFamily?)null,
                };
                sheet.Melee.DamageLevel = Level;
                sheet.Melee.AimLevel = Level;
                sheet.Melee.Power = (int)(melee.WeaponPower * setup.Offence);
                sheet.Melee.Accuracy = (int)(melee.WeaponAim * setup.Offence);
                var foe = new Foe { Sheet = sheet, Health = setup.Boss ? setup.BossPool : sheet.MaxHealth, ArrivesAt = at, NextAttack = at + 0.8 };
                foe.Fx.IsBoss = setup.Boss;
                return foe;
            }

            var foes = new List<Foe>();
            for (var i = 0; i < setup.Pack; ++i) foes.Add(Spawn(0));

            var pinned = setup.Health > 0;
            double health = pinned ? player.MaxHealth * setup.Health : player.MaxHealth, shield = 0, shieldUntil = 0, deadUntil = -1;
            var backHealer = setup.Healed > 0 ? Player(gear.Magic(Level), AttackStyle.Healing, null) : null;
            var nextBackHeal = 0.7;
            var lastStandUsed = false;
            var readyAt = new Dictionary<string, double>();
            double pauseUntil = 0, speedUntil = 0, speedAmount = 0, takenUntil = 0, takenAmount = 0, rushUntil = 0;
            double nextSwing = 0, nextTick = 1, time = 0;

            bool Here(Foe f) => f.Alive && f.ArrivesAt <= time;
            Foe Target() => foes.FirstOrDefault(Here);
            IEnumerable<Foe> Others(Foe not) => foes.Where(f => f != not && Here(f));

            void Hurt(Foe foe, double amount, bool strike = false)
            {
                if (!foe.Alive || amount <= 0) return;
                var dealt = Math.Min(amount, foe.Health);
                o.Damage += dealt;
                if (strike) o.Strike += dealt;
                foe.Health -= amount;
                if (foe.Alive) return;
                o.Kills++;
                state.Forget(foe);
                if (player.KillRush > 0) rushUntil = time + PathRules.RushSeconds;
                // Wildfire: the burn moves on to an enemy near
                if (player.BurnSpread > 0)
                {
                    var burn = foe.Fx.TakeBurn();
                    var next = burn != null ? Others(foe).FirstOrDefault() : null;
                    if (next != null) next.Fx.Burn(PathRules.BurnJump(burn.DamagePerSecond, player.BurnSpread), ElementRules.BurnSeconds);
                }
                foes[foes.IndexOf(foe)] = Spawn(time + Arrival);
            }

            void HealSelf(double amount, bool lifeSteal)
            {
                if (amount <= 0 || health <= 0) return;
                var missing = player.MaxHealth - health;
                if (lifeSteal && player.LifeStealShield > 0)
                {
                    var now = time <= shieldUntil ? shield : 0;
                    var after = PathRules.OverflowShield((int)amount, (int)missing, (int)now, player.MaxHealth, player.LifeStealShield);
                    if (after > now) { shield = after; shieldUntil = time + PathRules.OverflowShieldSeconds; }
                }
                var given = Math.Min(missing, amount * (1 + player.HealingReceived));
                if (!pinned) health += given;
                o.SelfHealed += given;
            }

            void PlayerTakes(double damage)
            {
                if (health <= 0 || damage <= 0) return;
                if (shield > 0 && time <= shieldUntil)
                {
                    var absorbed = Math.Min(shield, damage);
                    shield -= absorbed;
                    damage -= absorbed;
                    o.Absorbed += absorbed;
                }
                if (damage >= health && !lastStandUsed && build.HasLastStand)
                {
                    lastStandUsed = true;
                    damage = Math.Max(0, health - 1);
                }
                var lost = Math.Min(health, damage);
                o.Taken += lost;
                if (pinned) return;
                health -= lost;
                if (health > 0) return;
                o.Falls++;
                deadUntil = time + Downtime;
                state.Forget(null);
                state.Retort = false;
            }

            // the fighter beside the tank, or the tank in front of the healer: its damage is only counted
            void AllyTakes(double damage)
            {
                if (ally != null && damage > 0) o.AllyTaken += damage;
            }

            void HealAlly(double amount, bool strikes)
            {
                if (ally == null || amount <= 0) return;
                o.Healed += amount;
                if (player.HealShield > 0) o.Shielded += amount * player.HealShield;
                // Holy Fire: the heal also strikes an enemy
                if (strikes && player.HealStrike > 0)
                {
                    var foe = Target();
                    if (foe != null) Hurt(foe, PathRules.HealStrike((int)amount, player, foe.Sheet), strike: true);
                }
            }

            // a blow that landed on an enemy: its effects, then its damage
            void Landed(Foe foe, AttackResult result)
            {
                if (!result.Hit || !foe.Alive) return;
                if (result.Bleed >= 1) foe.Fx.Bleed(result.Bleed / 4.0, 4);
                if (result.Poison >= 1) foe.Fx.Venom(result.Poison, PathRules.PoisonSeconds);
                if (result.Procs.Count > 0)
                {
                    for (var i = 0; i < result.Procs.Count; ++i)
                    {
                        var proc = result.Procs[i];
                        if (proc.Element == Element.Fire) proc.Damage *= 1 + player.BurnBonus;
                        else if (proc.Element == Element.Poison) proc.Damage *= 1 + player.PoisonBonus;
                        else if (proc.Element == Element.Lightning) proc.Damage *= 1 + player.LightningJumpBonus;
                        result.Procs[i] = proc;
                    }
                    var chained = foe.Fx.ApplyProcs(result.Procs, random);
                    var next = chained >= 1 ? Others(foe).FirstOrDefault() : null;
                    if (next != null) Hurt(next, chained);
                }
                if (result.Healed > 0) HealSelf(result.Healed, true);
                Hurt(foe, result.Total);
            }

            void Prepare(Foe foe)
            {
                player.CurrentHealthShare = Math.Max(0, health) / player.MaxHealth;
                player.DamageMultiplier = time < rushUntil ? 1 + player.KillRush : 1;
                foe.Sheet.CurrentHealthShare = Math.Max(0, foe.Health) / (setup.Boss ? setup.BossPool : foe.Sheet.MaxHealth);
                foe.Sheet.DamageTakenMultiplier = time < foe.TakenUntil ? 1 + foe.TakenAmount : 1;
            }

            bool Fits(ActiveSkill active, SkillUse rule, Foe target)
            {
                switch (rule)
                {
                    case SkillUse.InFight: return target != null;
                    case SkillUse.OnBoss: return target != null && setup.Boss;
                    case SkillUse.TargetLow: return target != null && target.Health / (setup.Boss ? setup.BossPool : target.Sheet.MaxHealth) < 0.2;
                    case SkillUse.SelfHurt: return health / player.MaxHealth < 0.5;
                    case SkillUse.EnemiesNear: return foes.Count(Here) >= 3;
                    case SkillUse.AllyHurt: return setup.Healer;
                    default: return false; // Taunt, Cleanse and the pet's rules are not modelled
                }
            }

            bool Geared(SkillGear needs)
            {
                switch (needs)
                {
                    case SkillGear.Bow: return style == AttackStyle.Ranged;
                    case SkillGear.Staff: return style == AttackStyle.Magic;
                    case SkillGear.MeleeWeapon: case SkillGear.Shield: return style == AttackStyle.Melee;
                    case SkillGear.Pet: return false;
                    default: return true;
                }
            }

            bool UseSkill(Foe target)
            {
                if (time < pauseUntil) return false;
                foreach (var node in build.Actives())
                {
                    var active = node.Active;
                    if (readyAt.TryGetValue(node.Id, out var ready) && ready > time) continue;
                    if (!Geared(active.Needs) || !Fits(active, build.RuleOf(node), target)) continue;

                    var targets = new List<Foe>();
                    if (target != null)
                    {
                        switch (active.Targeting)
                        {
                            case SkillTargeting.Target: case SkillTargeting.FarthestEnemy: targets.Add(target); break;
                            case SkillTargeting.Cone: case SkillTargeting.Around: case SkillTargeting.AroundTarget: case SkillTargeting.Chain:
                                targets.Add(target);
                                targets.AddRange(Others(target).Take(Math.Max(0, active.MaxTargets - 1)));
                                break;
                        }
                    }
                    var cc = 1 + player.CrowdControlDuration;
                    var first = true;
                    foreach (var e in active.Effects)
                    {
                        switch (e.Kind)
                        {
                            case SkillEffectKind.Damage:
                                for (var t = 0; t < targets.Count; ++t)
                                {
                                    var foe = targets[t];
                                    // lightning that jumps (Chain Lightning): each jump harder by the jump bonus
                                    var jump = active.Targeting == SkillTargeting.Chain && e.Element == Element.Lightning ? Math.Pow(1 + player.LightningJumpBonus, t) : 1;
                                    for (var h = 0; h < Math.Max(1, e.Hits) && foe.Alive; ++h)
                                    {
                                        Prepare(foe);
                                        var options = new AttackOptions { Multiplier = e.Amount * jump, Element = e.Element, ArmorIgnored = e.ArmorIgnored };
                                        if (first) options = PathRules.Before(state, player, foe, options, false, time);
                                        var result = CombatMath.Attack(player, active.Style, foe.Sheet, random, options);
                                        if (first) PathRules.After(state, player, active.Style, foe, result, false, time);
                                        first = first && !result.Hit;
                                        Landed(foe, result);
                                    }
                                }
                                break;
                            case SkillEffectKind.DamageOverTime:
                                {
                                    var s = player.Style(active.Style);
                                    var perSecond = CombatMath.MaxHit(s.DamageLevel, s.EffectivePower) * 0.7 * player.DamageMultiplier * e.Amount / e.Seconds;
                                    foreach (var foe in targets)
                                    {
                                        if (e.Element == Element.Fire) foe.Fx.Burn(perSecond, e.Seconds);
                                        else if (e.Element == Element.Poison) foe.Fx.Poison(perSecond);
                                        else foe.Fx.Bleed(perSecond, e.Seconds);
                                    }
                                    break;
                                }
                            case SkillEffectKind.DamageTaken:
                                if (e.Amount > 0) foreach (var foe in targets) { foe.TakenUntil = time + e.Seconds; foe.TakenAmount = e.Amount; }
                                else { takenUntil = time + e.Seconds; takenAmount = e.Amount; }
                                break;
                            case SkillEffectKind.DamageDealt:
                                if (e.Amount < 0) foreach (var foe in targets) { foe.SoftUntil = time + e.Seconds; foe.SoftAmount = e.Amount; }
                                break;
                            case SkillEffectKind.AttackSpeed:
                                speedUntil = time + e.Seconds; speedAmount = e.Amount;
                                break;
                            case SkillEffectKind.Stun:
                            case SkillEffectKind.Freeze:
                                foreach (var foe in targets) foe.StunUntil = Math.Max(foe.StunUntil, time + e.Seconds * cc * (setup.Boss ? ElementRules.BossCrowdControlShare : 1));
                                break;
                            case SkillEffectKind.HealPercent:
                                HealSelf(player.MaxHealth * e.Amount, false);
                                break;
                            case SkillEffectKind.Heal:
                                HealAlly(CombatMath.Heal(player, random, e.Amount).Amount, true);
                                break;
                            case SkillEffectKind.HealOverTime:
                                hots.Add((CombatMath.Heal(player, random, e.Amount).Amount / e.Seconds, time + e.Seconds));
                                break;
                            case SkillEffectKind.Shield:
                                o.Shielded += CombatMath.Heal(player, random, e.Amount).Amount;
                                break;
                        }
                    }
                    readyAt[node.Id] = time + active.Cooldown * (1 - Math.Min(CombatMath.MaxCooldownReduction, player.CooldownReduction));
                    pauseUntil = time + SkillTrees.SharedPause;
                    return true;
                }
                return false;
            }

            while (time < Seconds)
            {
                var next = Math.Min(nextTick, Math.Max(nextSwing, deadUntil));
                if (backHealer != null) next = Math.Min(next, nextBackHeal);
                foreach (var foe in foes) if (foe.Alive) next = Math.Min(next, Math.Max(foe.NextAttack, foe.ArrivesAt));
                time = Math.Max(time, next);
                if (time >= Seconds) break;

                if (time >= nextTick)
                {
                    nextTick += 1;
                    foreach (var foe in foes.ToList())
                    {
                        if (!foe.Alive) continue;
                        var dot = foe.Fx.Tick(1);
                        if (dot.Total <= 0) continue;
                        var taken = time < foe.TakenUntil ? 1 + foe.TakenAmount : 1;
                        Hurt(foe, (dot.Physical * (1 - CombatMath.ArmorReduction(foe.Sheet.Armor)) + dot.Fire + dot.Poison) * taken);
                    }
                    if (health > 0 && player.CombatRegen > 0) HealSelf(player.MaxHealth * player.CombatRegen, false);
                    if (ally != null && setup.Healer)
                    {
                        foreach (var (perSecond, until) in hots) if (until > time - 1) HealAlly(perSecond, false);
                        hots.RemoveAll(h => h.until <= time);
                    }
                    if (health > 0) { o.HealthShare += health / player.MaxHealth; o.Samples++; }
                    continue;
                }

                if (health <= 0 && time >= deadUntil)
                {
                    health = player.MaxHealth;
                    lastStandUsed = false;
                    shield = 0;
                    nextSwing = time;
                    foreach (var foe in foes) foe.NextAttack = Math.Max(foe.NextAttack, time + 0.8);
                }

                // a healer behind the fighter (Vitality's second fight): a basic heal every cast
                if (backHealer != null && time >= nextBackHeal)
                {
                    nextBackHeal = time + setup.Healed;
                    if (health > 0) HealSelf(CombatMath.Heal(backHealer, random).Amount, false);
                }

                // the enemies' blows
                foreach (var foe in foes.ToList())
                {
                    if (!Here(foe) || foe.NextAttack > time) continue;
                    foe.NextAttack = time + EnemySwing / foe.Fx.AttackSpeedFactor;
                    if (time < foe.StunUntil || foe.Fx.CannotAct) { foe.NextAttack = Math.Max(time + 0.25, foe.StunUntil); continue; }
                    if (setup.Offence <= 0) continue;
                    foe.Sheet.DamageMultiplier = time < foe.SoftUntil ? 1 + foe.SoftAmount : 1;
                    foe.Sheet.CurrentHealthShare = 1;
                    var onAlly = setup.Healer ? true
                        : setup.Ally && (health <= 0 || random.Next() < 1 / (1 + Math.Max(0.1, player.ThreatMultiplier)));
                    if (onAlly)
                    {
                        var blow = CombatMath.Attack(foe.Sheet, AttackStyle.Melee, ally, random);
                        if (!blow.Hit) continue;
                        double damage = blow.Total;
                        // Shield of the Many: the tank takes its share in the ally's place, after its own armor
                        if (!setup.Healer && player.AllyGuard > 0 && health > player.MaxHealth * PathRules.GuardFloor)
                        {
                            var shared = PathRules.GuardShare(blow.Total, player.AllyGuard);
                            damage -= shared;
                            player.DamageTakenMultiplier = baseTaken * (time < takenUntil ? 1 + takenAmount : 1);
                            PlayerTakes(PathRules.GuardTaken(shared, player));
                        }
                        AllyTakes(damage);
                        continue;
                    }
                    if (setup.Healer || health <= 0) continue;
                    player.CurrentHealthShare = health / player.MaxHealth;
                    player.DamageTakenMultiplier = baseTaken * (time < takenUntil ? 1 + takenAmount : 1);
                    var result = CombatMath.Attack(foe.Sheet, AttackStyle.Melee, player, random);
                    if (!result.Hit) continue;
                    if (result.Blocked) PathRules.Blocked(state, player);
                    if (result.Reflected > 0) Hurt(foe, result.Reflected);
                    PlayerTakes(result.Total);
                }

                // the player's swing, shot, spell or heal
                if (health > 0 && time >= nextSwing)
                {
                    PathRules.Rest(state, time);
                    var target = Target();
                    if (target != null) Prepare(target);
                    var acted = UseSkill(target);
                    if (!acted && setup.Healer)
                    {
                        HealAlly(CombatMath.Heal(player, random).Amount, true);
                        acted = true;
                    }
                    if (!acted && target != null)
                    {
                        var options = PathRules.Before(state, player, target, AttackOptions.Basic, true, time);
                        var result = CombatMath.Attack(player, style, target.Sheet, random, options);
                        var blow = PathRules.After(state, player, style, target, result, true, time);
                        var others = blow.Splash > 0 || blow.Second > 0 ? Others(target).ToList() : null;
                        Landed(target, result);
                        if (blow.Splash > 0) foreach (var foe in others) Hurt(foe, blow.Splash);
                        if (blow.Second > 0 && others.Count > 0) Hurt(others[0], blow.Second);
                        acted = true;
                    }
                    var speed = Math.Min(CombatMath.MaxAttackSpeed, player.AttackSpeed + PathRules.ComboSpeed(state, player)) * (time < speedUntil ? 1 + speedAmount : 1);
                    if (style == AttackStyle.Magic || style == AttackStyle.Healing) speed *= player.CastSpeed;
                    nextSwing = acted ? time + Swing / Math.Max(0.2, speed) : foes.Where(f => f.Alive).Select(f => f.ArrivesAt).DefaultIfEmpty(time + 0.5).Min();
                    if (nextSwing <= time) nextSwing = time + 0.1;
                }
            }
            return o;
        }
    }
}

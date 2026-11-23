using RavenNest.Combat;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RavenNest.CombatSimulator
{
    /// <summary>
    ///     The Legendaries (the game's docs/legendary-design.md, section 8): a level 999 player in the standard kit for
    ///     the Legendary's style, fighting with the best normal weapon of the type, then with the Legendary at rank 1 and
    ///     rank 5 (its numbers worked out from that best weapon as the server does, its fortress touch and its power).
    ///
    ///     The fights, 300 s each:
    ///       one    enemies of the player's level one after another (three quarters of the kit's armor, a quarter of its
    ///              power; the next arrives 2 s after a kill), the player held at 90% health
    ///       pack   four of them at once, held at 90%
    ///       boss   one boss whose pool lasts about 90 s for the normal weapon, held at 90%
    ///       room   sixteen to clear, four at a time, hitting at 30% of the kit's power, health not held (a fall costs
    ///              10 s): the time to clear it, the damage taken after shields and the falls
    ///     Positions are not modelled: every enemy is in reach of the swarm, the blast and the aegis.
    /// </summary>
    public static class LegendaryScenarios
    {
        private const int Level = 999;
        private const double Seconds = 300;
        private const double Swing = 1.5;
        private const double EnemySwing = 2.0;
        private const double Arrival = 2.0;
        private const double Downtime = 10;
        private const double Step = 0.05;

        private sealed class Foe
        {
            public CombatSheet Sheet;
            public double Health, NextAttack, StunUntil, ArrivesAt;
            public bool Alive => Health > 0;
        }

        public sealed class Outcome
        {
            public double Damage, Taken, SelfHealed, Shield, Falls, Kills, Feared, Swarms, Blasts, Time;
        }

        private sealed class Fight
        {
            public string Name;
            public int Pack = 1;
            public bool Boss;
            public double Offence = 0.25;
            public double Health = 0.9; // 0: not held
            /// <summary>Enemies in all (a room to clear), 0 for endless.</summary>
            public int Total;
        }

        private static readonly Fight[] Fights =
        {
            new Fight { Name = "one" },
            new Fight { Name = "pack", Pack = 4 },
            new Fight { Name = "boss", Boss = true, Offence = 1 },
            new Fight { Name = "room", Pack = 4, Offence = 0.3, Health = 0, Total = 16 },
        };

        /// <summary>The fortress touch each keeps (GameData.DarkFortress' Extra).</summary>
        private static readonly Dictionary<string, string> Touch = new Dictionary<string, string>
        {
            ["soulreaver"] = "LIFE_STEAL:3%",
            ["hellhorn-mace"] = "FIRE_DAMAGE:15%",
            ["batwing-staff"] = "LIFE_STEAL:3%",
            ["spiked-bulwark"] = "THORNS:8%",
            ["fortress-hand-cannon"] = "CRIT_CHANCE:5%",
        };

        public static void Run(GearPicker all, int runs, string[] overrides = null)
        {
            // "hellhorn-mace=FIRE_DAMAGE:15%" tries another fortress touch without a rebuild
            foreach (var o in overrides ?? Array.Empty<string>())
            {
                var eq = o.IndexOf('=');
                if (eq > 0 && Touch.ContainsKey(o.Substring(0, eq))) { Touch[o.Substring(0, eq)] = o.Substring(eq + 1); Console.WriteLine("Trying " + o); }
            }
            System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
            Console.WriteLine($"Legendaries: a level {Level} player in the standard kit, {Seconds:0} s a fight, {runs} runs. Each cell is the Legendary as a multiple of the best normal item of its type in the same kit.");
            Console.WriteLine("one / pack / boss: damage dealt (held at 90% health). room: sixteen to clear four at a time at 30% power, health not held: time to clear, damage taken after shields, falls (the normal weapon's in brackets).");
            Console.WriteLine();
            var standard = all.Standard();
            foreach (var legendary in Legendary.All)
            {
                var style = legendary.Skill == LegendarySkill.Magic ? AttackStyle.Magic : legendary.Skill == LegendarySkill.Ranged ? AttackStyle.Ranged : AttackStyle.Melee;
                var kit = style == AttackStyle.Magic ? standard.Magic(Level) : style == AttackStyle.Ranged ? standard.Ranged(Level) : standard.Melee(Level);
                var best = all.BestOfType(legendary.ItemType);
                if (best == null) { Console.WriteLine(legendary.Name + ": no item of its type"); continue; }
                var normal = Swap(kit, legendary, best, 0, null);
                var one = Swap(kit, legendary, best, 1, Touch[legendary.Key]);
                var five = Swap(kit, legendary, best, 5, Touch[legendary.Key]);

                Console.WriteLine($"{legendary.Name} ({legendary.PowerName}), best normal: {best.Name}");
                Console.WriteLine($"  {"fight",-6} {"rank 1",-28} {"rank 5",-28}");
                var bossPool = 0.0;
                foreach (var fight in Fights)
                {
                    if (fight.Boss && bossPool <= 0)
                    {
                        // a pool the normal weapon needs about 90 s for
                        var probe = Average(runs / 4 + 1, r => One(normal, style, null, 0, new Fight { Name = "probe", Boss = false }, 0, r));
                        bossPool = probe.Damage / Seconds * 90;
                    }
                    var n = Average(runs, r => One(normal, style, null, 0, fight, bossPool, r));
                    var r1 = Average(runs, r => One(one, style, legendary, 1, fight, bossPool, r));
                    var r5 = Average(runs, r => One(five, style, legendary, 5, fight, bossPool, r));
                    Console.WriteLine($"  {fight.Name,-6} {Cell(n, r1, fight),-28} {Cell(n, r5, fight),-28}{Extra(r1, r5)}");
                }
                Console.WriteLine();
            }
        }

        private static string Cell(Outcome n, Outcome l, Fight fight)
        {
            var dmg = n.Damage > 0 ? l.Damage / n.Damage : 0;
            if (fight.Total <= 0) return $"{dmg:0.000}";
            var time = n.Time > 0 ? l.Time / n.Time : 0;
            var taken = n.Taken > 0 ? l.Taken / n.Taken : 0;
            return $"time {time:0.00} taken {taken:0.00} falls {l.Falls:0.00} ({n.Falls:0.00})";
        }

        private static string Extra(Outcome r1, Outcome r5)
        {
            var parts = new List<string>();
            if (r5.SelfHealed > 0) parts.Add($"healed {r1.SelfHealed:0}/{r5.SelfHealed:0}");
            if (r5.Feared > 0) parts.Add($"feared {r1.Feared:0.0}/{r5.Feared:0.0}");
            if (r5.Swarms > 0) parts.Add($"swarms {r1.Swarms:0.0}/{r5.Swarms:0.0}");
            if (r5.Blasts > 0) parts.Add($"blasts {r1.Blasts:0.0}/{r5.Blasts:0.0}");
            if (r5.Shield > 0) parts.Add($"ally shield/min {r1.Shield / Seconds * 60:0}/{r5.Shield / Seconds * 60:0}");
            return parts.Count == 0 ? "" : "  " + string.Join(", ", parts);
        }

        private static Outcome Average(int runs, Func<int, Outcome> run)
        {
            var list = Enumerable.Range(0, Math.Max(1, runs)).Select(run).ToList();
            return new Outcome
            {
                Damage = list.Average(x => x.Damage), Taken = list.Average(x => x.Taken), SelfHealed = list.Average(x => x.SelfHealed),
                Shield = list.Average(x => x.Shield), Falls = list.Average(x => x.Falls), Kills = list.Average(x => x.Kills),
                Feared = list.Average(x => x.Feared), Swarms = list.Average(x => x.Swarms), Blasts = list.Average(x => x.Blasts), Time = list.Average(x => x.Time),
            };
        }

        /// <summary>The kit with its weapon (or shield) of the type replaced: the best normal one, or the Legendary at a rank.</summary>
        private static Loadout Swap(Loadout kit, LegendaryInfo legendary, ItemData best, int rank, string touch)
        {
            var copy = new Loadout();
            foreach (var i in kit.Items)
            {
                var sameSlot = legendary.IsShield ? i.Type == 13 : i == kit.Weapon;
                if (!sameSlot) copy.Items.Add(i);
            }
            var item = new ItemData
            {
                Name = rank == 0 ? best.Name : legendary.Name + " rank " + rank,
                Type = best.Type,
                Level = best.Level,
                WeaponAim = Scale(best.WeaponAim, rank), WeaponPower = Scale(best.WeaponPower, rank),
                MagicAim = Scale(best.MagicAim, rank), MagicPower = Scale(best.MagicPower, rank),
                RangedAim = Scale(best.RangedAim, rank), RangedPower = Scale(best.RangedPower, rank),
                ArmorPower = Scale(best.ArmorPower, rank),
            };
            if (rank > 0)
            {
                var stats = GearStats.FromLegacy(item.WeaponAim, item.WeaponPower, item.RangedAim, item.RangedPower, item.MagicAim, item.MagicPower, item.ArmorPower);
                stats.Add(GearStats.Parse(touch));
                stats.Add(Legendary.Stats(legendary.Power, rank));
                item.Stats = stats.ToString();
            }
            copy.Items.Add(item);
            return copy;
        }

        private static int Scale(int best, int rank) => rank == 0 ? best : Legendary.AtRank(Legendary.Derive(best), rank);

        private static CombatSheet Player(Loadout kit, AttackStyle style)
        {
            var sheet = new CombatSheet { CombatLevel = Level, MaxHealth = CombatMath.PlayerMaxHealth(Level, kit.ArmorHealth), DefenseLevel = Level, Armor = kit.ArmorPower, IsPlayer = true };
            var s = sheet.Style(style);
            s.DamageLevel = Level;
            s.AimLevel = Level;
            s.Power = style == AttackStyle.Ranged ? kit.RangedPower : style == AttackStyle.Magic ? kit.MagicPower : kit.WeaponPower;
            s.Accuracy = style == AttackStyle.Ranged ? kit.RangedAim : style == AttackStyle.Magic ? kit.MagicAim : kit.WeaponAim;
            kit.ApplyExtras(sheet);
            return sheet;
        }

        private static Outcome One(Loadout kit, AttackStyle style, LegendaryInfo legendary, int rank, Fight fight, double bossPool, int seed)
        {
            var o = new Outcome();
            var random = new SeededCombatRandom(seed * 7919 + 17);
            var rng = new Random(seed * 31 + 5);
            var player = Player(kit, style);
            var power = legendary != null ? Legendary.Values(legendary.Power, rank) : default;
            var has = legendary != null;
            var state = new LegendaryState();
            var melee = kit; // the enemies' numbers come from the kit, as in the capstone tables
            var health = (double)player.MaxHealth;
            var shield = 0.0;
            var shieldUntil = 0.0;
            var downUntil = -1.0;

            Foe Spawn(double at)
            {
                var sheet = new CombatSheet
                {
                    CombatLevel = Level, DefenseLevel = Level, IsBoss = fight.Boss,
                    Armor = (int)(melee.ArmorPower * (fight.Boss ? 1 : 0.75)),
                    MaxHealth = fight.Boss ? (int)Math.Min(int.MaxValue, Math.Max(1, bossPool)) : CombatMath.MaxHealth(Level),
                };
                sheet.Melee.DamageLevel = Level;
                sheet.Melee.AimLevel = Level;
                var weaponPower = Math.Max(melee.WeaponPower, Math.Max(melee.RangedPower, melee.MagicPower));
                sheet.Melee.Power = (int)(weaponPower * fight.Offence);
                sheet.Melee.Accuracy = (int)(weaponPower * fight.Offence);
                return new Foe { Sheet = sheet, Health = sheet.MaxHealth, ArrivesAt = at, NextAttack = at + 0.8 };
            }

            var foes = new List<Foe>();
            for (var i = 0; i < fight.Pack; ++i) foes.Add(Spawn(0));
            var spawned = fight.Pack;
            var next = 0.0;
            var now = 0.0;

            o.Time = Seconds;
            for (var t = 0.0; t < Seconds; t += Step)
            {
                now = t;
                if (fight.Total > 0 && foes.All(f => !f.Alive)) { o.Time = t; break; }
                player.CurrentHealthShare = fight.Health > 0 ? fight.Health : Math.Max(0.01, health / player.MaxHealth);
                if (t < downUntil) continue;

                // Iron Aegis: a shield on the wielder (and the same on every ally near, counted as Shield)
                if (has && legendary.Power == LegendaryPower.IronAegis && foes.Any(f => f.Alive && f.ArrivesAt <= t) && LegendaryRules.Aegis(state, t))
                {
                    var s = LegendaryRules.AegisShield(power, player.MaxHealth);
                    shield = Math.Max(t < shieldUntil ? shield : 0, s);
                    shieldUntil = t + LegendaryRules.AegisSeconds;
                    o.Shield += s;
                }

                // the player's blow
                var target = foes.FirstOrDefault(f => f.Alive && f.ArrivesAt <= t);
                if (target != null && t >= next)
                {
                    var options = new AttackOptions();
                    var blast = has && legendary.Power == LegendaryPower.ThunderShot && LegendaryRules.BlastNext(state);
                    if (blast) options.Multiplier = 1 + power.BlastBonus;
                    var res = CombatMath.Attack(player, style, target.Sheet, random, options);
                    Hit(target, res.Total);
                    o.Damage += res.Total;
                    if (res.Healed > 0) Heal(res.Healed);

                    if (has && res.Hit)
                    {
                        switch (legendary.Power)
                        {
                            case LegendaryPower.Dread:
                                if (target.Alive && LegendaryRules.Feared(power, fight.Boss, rng.NextDouble())) { target.StunUntil = Math.Max(target.StunUntil, t + power.FearSeconds); o.Feared++; }
                                break;
                            case LegendaryPower.BatSwarm:
                                var bite = LegendaryRules.Swarm(state, power, res.Total, t);
                                if (bite > 0)
                                {
                                    o.Swarms++;
                                    foreach (var f in foes.Where(f => f.Alive && f.ArrivesAt <= t).Take(power.SwarmBites).ToList()) { Hit(f, bite); o.Damage += bite; }
                                }
                                break;
                            case LegendaryPower.ThunderShot:
                                if (LegendaryRules.ShotHit(state))
                                {
                                    o.Blasts++;
                                    var splash = LegendaryRules.BlastSplash(power, res.Total);
                                    foreach (var f in foes.Where(f => f != target && f.Alive && f.ArrivesAt <= t).Take(LegendaryRules.BlastTargets).ToList()) { Hit(f, splash); o.Damage += splash; }
                                    if (!fight.Boss && target.Alive) target.StunUntil = Math.Max(target.StunUntil, t + power.StaggerSeconds);
                                }
                                break;
                        }
                    }
                    next = t + Swing / Math.Max(0.2, player.AttackSpeed);
                }

                // the enemies' blows
                for (var fi = 0; fi < foes.Count; ++fi)
                {
                    var f = foes[fi];
                    if (!f.Alive || f.ArrivesAt > t || t < f.NextAttack || t < f.StunUntil) continue;
                    f.NextAttack = t + EnemySwing;
                    var res = CombatMath.Attack(f.Sheet, AttackStyle.Melee, player, random);
                    if (res.Reflected > 0) { Hit(f, res.Reflected); o.Damage += res.Reflected; }
                    var dmg = (double)res.Total;
                    if (t < shieldUntil && shield > 0)
                    {
                        var absorbed = Math.Min(shield, dmg);
                        shield -= absorbed;
                        dmg -= absorbed;
                    }
                    o.Taken += dmg;
                    if (fight.Health <= 0)
                    {
                        health -= dmg;
                        if (health <= 0)
                        {
                            o.Falls++;
                            health = player.MaxHealth;
                            downUntil = t + Downtime;
                            foreach (var g in foes) g.NextAttack = Math.Max(g.NextAttack, downUntil + 0.8);
                            break;
                        }
                    }
                }
            }
            return o;

            void Heal(double amount)
            {
                var before = health;
                health = Math.Min(player.MaxHealth, health + amount);
                o.SelfHealed += fight.Health > 0 ? amount : health - before;
            }

            void Hit(Foe f, int amount)
            {
                if (!f.Alive || amount <= 0) return;
                f.Health -= amount;
                if (f.Alive) return;
                o.Kills++;
                if (has && legendary.Power == LegendaryPower.SoulHarvest) Heal(LegendaryRules.KillHeal(power, player.MaxHealth));
                // the next one arrives 2 s later in its place
                if (fight.Total > 0 && spawned >= fight.Total) return; // the room is emptying
                spawned++;
                var i = foes.IndexOf(f);
                if (i >= 0) foes[i] = Spawn(now + Arrival);
            }
        }
    }
}

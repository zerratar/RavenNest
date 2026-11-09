using RavenNest.Combat;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RavenNest.CombatSimulator
{
    /// <summary>
    ///     Skill tree builds against one enemy (the game's docs/combat-skills-design.md): a player of a level in the
    ///     standard kit for its style, with a build's passives and its loadout used on cooldown (top first, 1.5 s apart,
    ///     instead of a swing), against an enemy of the same level geared like the island enemies (75% of the armour,
    ///     25% of the power and accuracy of the kit). Damage over two minutes, as a multiple of the same player with no
    ///     build. Single target only: area skills hit one, rules that need three enemies never fire. Builds that cost
    ///     more points than the level earns are marked with *.
    /// </summary>
    public static class BuildScenarios
    {
        private static readonly int[] Levels = { 100, 300, 600, 999 };
        private const double Seconds = 120;

        private sealed class Build
        {
            public string Name;
            public AttackStyle Style;
            public string Text;
            public bool Boss;
        }

        private static readonly Build[] Builds =
        {
            new Build { Name = "Might", Style = AttackStyle.Melee, Text = "brute-force:5;cleave:1;crushing-blow:1;rend:5;whirlwind:1;executioner:5;leap:1;titan:1|crushing-blow,leap,cleave,whirlwind" },
            new Build { Name = "Precision", Style = AttackStyle.Melee, Text = "keen-eye:5;sharp-edge:5;expose-weakness:1;deadly:5;flurry:1;swift:5;disarm:1;precision-strikes:1|flurry,expose-weakness" },
            new Build { Name = "Hunter (boss)", Style = AttackStyle.Melee, Boss = true, Text = "big-game:5;mark-prey:1;execute:1;treasure-hunter:5;apex-predator:1;brute-force:5;crushing-blow:1|mark-prey,crushing-blow,execute" },
            new Build { Name = "Marksman", Style = AttackStyle.Ranged, Text = "steady-aim:5;multishot:1;piercing-shot:1;eagle-eye:5;hunters-pace:5;rapid-fire:1|rapid-fire,piercing-shot,multishot" },
            new Build { Name = "Elements", Style = AttackStyle.Magic, Text = "arcane-power:5;fireball:1;chain-lightning:1;unravel:5;meteor:1;quickened-casting:5|meteor,chain-lightning,fireball" },
        };

        public static void Run(GearPicker allGear, int fights)
        {
            var gear = allGear.Standard();
            Console.WriteLine($"Skill tree builds, one enemy of the player's level, {Seconds:0} s, {fights} runs: damage as a multiple of no build.");
            Console.WriteLine($"{"Build",-15} {"Cost",5} | " + string.Join(" | ", Levels.Select(l => $"{"Lv " + l,9}")));
            Console.WriteLine(new string('-', 26 + Levels.Length * 12));
            var random = new SeededCombatRandom(41);
            foreach (var build in Builds)
            {
                var parsed = SkillBuild.Parse(build.Text);
                var cells = Levels.Select(level =>
                {
                    var earned = SkillTrees.EarnedPoints(level * 8);
                    var plain = Enumerable.Range(0, fights).Average(_ => Damage(gear, level, build.Style, new SkillBuild(), build.Boss, random));
                    var with = Enumerable.Range(0, fights).Average(_ => Damage(gear, level, build.Style, parsed, build.Boss, random));
                    return $"{with / Math.Max(1, plain),7:0.00}x{(parsed.Spent > earned ? "*" : " ")}";
                });
                Console.WriteLine($"{build.Name,-15} {parsed.Spent,5} | " + string.Join(" | ", cells.Select(c => $"{c,9}")));
            }
            Console.WriteLine();
            Console.WriteLine("Points earned with every combat skill at the level: " + string.Join(", ", Levels.Select(l => "Lv " + l + ": " + SkillTrees.EarnedPoints(l * 8))));
        }

        private static Loadout Kit(GearPicker gear, int level, AttackStyle style) =>
            style == AttackStyle.Ranged ? gear.Ranged(level) : style == AttackStyle.Magic ? gear.Magic(level) : gear.Melee(level);

        private static double Damage(GearPicker gear, int level, AttackStyle style, SkillBuild build, bool boss, ICombatRandom random)
        {
            var kit = Kit(gear, level, style);
            var player = new CombatSheet { CombatLevel = level, MaxHealth = CombatMath.PlayerMaxHealth(level, kit.ArmorHealth), DefenseLevel = level, Armor = kit.ArmorPower, IsPlayer = true };
            var s = player.Style(style);
            s.DamageLevel = level;
            s.AimLevel = level;
            s.Power = style == AttackStyle.Ranged ? kit.RangedPower : style == AttackStyle.Magic ? kit.MagicPower : kit.WeaponPower;
            s.Accuracy = style == AttackStyle.Ranged ? kit.RangedAim : style == AttackStyle.Magic ? kit.MagicAim : kit.WeaponAim;
            kit.ApplyExtras(player);
            build.Passives().ApplyExtras(player, null);

            var enemyKit = gear.Melee(level);
            var enemy = new CombatSheet { CombatLevel = level, MaxHealth = int.MaxValue, DefenseLevel = level, Armor = (int)(enemyKit.ArmorPower * 0.75), IsBoss = boss };

            var readyAt = new Dictionary<string, double>();
            var takenUntil = 0.0; var takenAmount = 0.0;   // Expose Weakness, Mark Prey on the enemy
            var speedUntil = 0.0; var speedAmount = 0.0;   // Rapid Fire
            var pauseUntil = 0.0;
            var dots = new List<(double perSecond, double until)>();
            var total = 0.0;
            var time = 0.0;
            var swing = style == AttackStyle.Melee || style == AttackStyle.Ranged ? 1.5 : 1.5;
            while (time < Seconds)
            {
                enemy.DamageTakenMultiplier = time < takenUntil ? 1 + takenAmount : 1;
                var used = false;
                if (time >= pauseUntil)
                {
                    foreach (var node in build.Actives())
                    {
                        var active = node.Active;
                        if (readyAt.TryGetValue(node.Id, out var ready) && ready > time) continue;
                        if (active.Use == SkillUse.EnemiesNear || active.Use == SkillUse.AllyHurt || active.Use == SkillUse.SelfHurt || active.Use == SkillUse.AllyHarmed || active.Use == SkillUse.EnemyOnAlly || active.Use == SkillUse.TargetLow) continue;
                        if (active.Use == SkillUse.OnBoss && !boss) continue;
                        if (active.Needs == SkillGear.Bow && style != AttackStyle.Ranged) continue;
                        if (active.Needs == SkillGear.Staff && style != AttackStyle.Magic) continue;
                        if (active.Needs == SkillGear.MeleeWeapon && style != AttackStyle.Melee) continue;
                        if (active.Needs == SkillGear.Shield) continue;
                        foreach (var e in active.Effects)
                        {
                            switch (e.Kind)
                            {
                                case SkillEffectKind.Damage:
                                    for (var h = 0; h < Math.Max(1, e.Hits); ++h)
                                        total += CombatMath.Attack(player, active.Style, enemy, random, new AttackOptions { Multiplier = e.Amount, Element = e.Element, ArmorIgnored = e.ArmorIgnored }).Total;
                                    break;
                                case SkillEffectKind.DamageOverTime:
                                    var sty = player.Style(active.Style);
                                    dots.Add((CombatMath.MaxHit(sty.DamageLevel, sty.EffectivePower) * 0.7 * e.Amount / e.Seconds, time + e.Seconds));
                                    break;
                                case SkillEffectKind.DamageTaken when e.Amount > 0:
                                    takenUntil = time + e.Seconds; takenAmount = e.Amount;
                                    break;
                                case SkillEffectKind.AttackSpeed:
                                    speedUntil = time + e.Seconds; speedAmount = e.Amount;
                                    break;
                            }
                        }
                        readyAt[node.Id] = time + active.Cooldown;
                        pauseUntil = time + SkillTrees.SharedPause;
                        used = true;
                        break;
                    }
                }

                if (!used) total += CombatMath.Attack(player, style, enemy, random).Total;
                var speed = player.AttackSpeed * (time < speedUntil ? 1 + speedAmount : 1) * (style == AttackStyle.Magic ? player.CastSpeed : 1);
                var step = swing / Math.Max(0.2, speed);
                foreach (var (perSecond, until) in dots) total += perSecond * Math.Max(0, Math.Min(step, until - time));
                dots.RemoveAll(d => d.until <= time);
                time += step;
            }
            return total;
        }
    }
}

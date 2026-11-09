using RavenNest.Combat;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RavenNest.CombatSimulator
{
    /// <summary>
    ///     Battle pets beside their owner (the game's docs/battle-pets-design.md), prepared before the models: an owner in
    ///     the standard melee kit with no skill build, a role stand-in pet at the owner's level (evolved at 30 and 100),
    ///     against one enemy of the same level with a share of the kit's armour and offence, for two minutes. The enemy
    ///     picks whom to hit with the game's threat table. Compared with the owner alone:
    ///     damage added, how many of the enemy's hits the pet takes, the owner's damage taken, the share of it the pet
    ///     heals back, and how often the pet is knocked out. Targets: damage +30 to 40%, a tank takes most hits, a healer
    ///     gives back a third or more of what its owner takes, support +10 to 20% damage.
    /// </summary>
    public static class PetScenarios
    {
        private static readonly int[] Levels = { 10, 50, 100, 300, 600, 999 };
        private const double Seconds = 120;
        private const double OwnerSwing = 1.5;
        private const double EnemySwing = 1.5;

        private sealed class Result
        {
            public double OwnerDamage, PetDamage, OwnerTaken, PetTaken, Healed, OwnerFalls;
            public int EnemyHits, HitsOnPet, Knockouts;
        }

        public static void Run(GearPicker allGear, int fights, double armourShare, double offenceShare)
        {
            System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
            var gear = allGear.Standard();
            Console.WriteLine($"Battle pets (role stand-ins) beside an owner in the standard melee kit, one enemy of the same level with");
            Console.WriteLine($"{armourShare:P0} of the kit's armour and {offenceShare:P0} of its power and accuracy, {Seconds:0} s, {fights} runs per cell.");
            Console.WriteLine("Per cell: damage added, share of the enemy's hits on the pet, the owner's damage taken, healed back, knockouts per run.");
            Console.WriteLine();
            var random = new SeededCombatRandom(53);
            foreach (var species in BattlePets.Archetypes.Where(a => a.Role != PetRole.Gatherer))
            {
                Console.WriteLine($"{species.NameAt(0)} ({species.Role}, skills: {string.Join(", ", species.Skills)})");
                Console.WriteLine($"  {"Lv",4} {"Stage",5} | {"dmg",7} {"on pet",7} {"taken",7} {"healed",7} {"KOs",5} | {"pet hp",7} {"owner hp",8}");
                foreach (var level in Levels)
                {
                    var stage = level >= BattlePets.EvolveLevels[1] ? 2 : level >= BattlePets.EvolveLevels[0] ? 1 : 0;
                    var alone = Average(Enumerable.Range(0, fights).Select(_ => Fight(gear, level, null, stage, armourShare, offenceShare, random)).ToList());
                    var with = Average(Enumerable.Range(0, fights).Select(_ => Fight(gear, level, species, stage, armourShare, offenceShare, random)).ToList());
                    var added = (with.OwnerDamage + with.PetDamage) / Math.Max(1, alone.OwnerDamage) - 1;
                    var onPet = with.EnemyHits > 0 ? (double)with.HitsOnPet / with.EnemyHits : 0;
                    var taken = with.OwnerTaken / Math.Max(1, alone.OwnerTaken) - 1;
                    var healed = with.Healed / Math.Max(1, with.OwnerTaken + with.PetTaken);
                    var petHp = species.Sheet(level, stage).MaxHealth;
                    Console.WriteLine($"  {level,4} {stage,5} | {added,7:+0%;-0%;0%} {onPet,7:0%} {taken,7:+0%;-0%;0%} {healed,7:0%} {with.Knockouts / (double)Math.Max(1, fights),5:0.0} | {petHp,7} {CombatMath.MaxHealth(level),8}");
                }
                Console.WriteLine();
            }

            Console.WriteLine("Gatherer pets do not fight; their skills: " + string.Join(", ", BattlePets.Skills.Where(s => s.Role == PetRole.Gatherer).Select(s => s.Name + " (" + s.Description + ")")));
        }

        private static Result Average(List<Result> runs) => new Result
        {
            OwnerDamage = runs.Average(r => r.OwnerDamage),
            PetDamage = runs.Average(r => r.PetDamage),
            OwnerTaken = runs.Average(r => r.OwnerTaken),
            PetTaken = runs.Average(r => r.PetTaken),
            Healed = runs.Average(r => r.Healed),
            OwnerFalls = runs.Average(r => r.OwnerFalls),
            EnemyHits = runs.Sum(r => r.EnemyHits),
            HitsOnPet = runs.Sum(r => r.HitsOnPet),
            Knockouts = runs.Sum(r => r.Knockouts),
        };

        private const int Owner = 0, Pet = 1;

        private static Result Fight(GearPicker gear, int level, PetSpecies species, int stage, double armourShare, double offenceShare, ICombatRandom random)
        {
            var kit = gear.Melee(level);
            var owner = new CombatSheet { CombatLevel = level, MaxHealth = CombatMath.PlayerMaxHealth(level, kit.ArmorHealth), DefenseLevel = level, Armor = kit.ArmorPower, IsPlayer = true };
            owner.Melee.DamageLevel = level;
            owner.Melee.AimLevel = level;
            owner.Melee.Power = kit.WeaponPower;
            owner.Melee.Accuracy = kit.WeaponAim;
            kit.ApplyExtras(owner);

            var enemy = new CombatSheet { CombatLevel = level, MaxHealth = int.MaxValue, DefenseLevel = level, Armor = (int)(kit.ArmorPower * armourShare) };
            enemy.Melee.DamageLevel = level;
            enemy.Melee.AimLevel = level;
            enemy.Melee.Power = (int)(kit.WeaponPower * offenceShare);
            enemy.Melee.Accuracy = (int)(kit.WeaponAim * offenceShare);

            var pet = species?.Sheet(level, stage);
            var interval = species?.Stats.AttackInterval ?? 2;
            var petSkills = species == null ? new List<PetSkill>() :
                species.SkillsAt(level, stage).Where(s => s.IsActive).Take(BattlePets.SkillSlots(level, stage)).ToList();

            var result = new Result();
            var threat = new ThreatTable<object>();
            object ownerKey = "owner", petKey = "pet";
            object target = ownerKey;
            var ownerHealth = (double)owner.MaxHealth;
            var petHealth = pet != null ? (double)pet.MaxHealth : 0;
            var knockedOutUntil = -1.0;
            bool PetUp(double t) => pet != null && t >= knockedOutUntil;

            var readyAt = new Dictionary<string, double>();
            double petPauseUntil = 0, petSpeedUntil = 0, petSpeed = 0, ownerSpeedUntil = 0, ownerSpeed = 0;
            double hardenUntil = 0, harden = 0, hexUntil = 0, hex = 0, stunUntil = 0;
            var dots = new List<(double perSecond, double until)>();
            var heals = new List<(double perSecond, double until)>();

            double nextOwner = 0, nextPet = 0.3, nextEnemy = 0.8, time = 0;
            while (time < Seconds)
            {
                var next = Math.Min(nextOwner, Math.Min(pet != null ? nextPet : double.MaxValue, nextEnemy));
                var step = next - time;
                threat.Tick(step);
                foreach (var (perSecond, until) in dots) { var d = perSecond * Math.Max(0, Math.Min(step, until - time)); result.PetDamage += d; threat.AddDamage(petKey, d, pet?.ThreatMultiplier ?? 1); }
                foreach (var (perSecond, until) in heals) { var h = perSecond * Math.Max(0, Math.Min(step, until - time)); result.Healed += Heal(ref ownerHealth, owner.MaxHealth, h); }
                dots.RemoveAll(d => d.until <= time);
                heals.RemoveAll(h => h.until <= time);
                time = next;
                if (time >= Seconds) break;
                enemy.DamageTakenMultiplier = time < hexUntil ? 1 + hex : 1;

                if (time == nextOwner)
                {
                    var hit = CombatMath.Attack(owner, AttackStyle.Melee, enemy, random).Total;
                    result.OwnerDamage += hit;
                    threat.AddDamage(ownerKey, hit, owner.ThreatMultiplier);
                    nextOwner = time + OwnerSwing / (owner.AttackSpeed * (time < ownerSpeedUntil ? 1 + ownerSpeed : 1));
                }
                else if (pet != null && time == nextPet)
                {
                    if (!PetUp(time)) { nextPet = knockedOutUntil; continue; }
                    if (petHealth <= 0) { petHealth = pet.MaxHealth; }
                    var used = false;
                    if (time >= petPauseUntil)
                    {
                        foreach (var skill in petSkills)
                        {
                            var a = skill.Active;
                            if (readyAt.TryGetValue(skill.Id, out var ready) && ready > time) continue;
                            var ok = a.Use switch
                            {
                                SkillUse.InFight => true,
                                SkillUse.EnemyOnAlly => target == ownerKey,
                                SkillUse.SelfHurt => petHealth < pet.MaxHealth * 0.5,
                                SkillUse.AllyHurt => ownerHealth < owner.MaxHealth * 0.6 || petHealth < pet.MaxHealth * 0.6,
                                _ => false,
                            };
                            if (!ok) continue;
                            foreach (var e in a.Effects)
                            {
                                switch (e.Kind)
                                {
                                    case SkillEffectKind.Damage:
                                        var d = CombatMath.Attack(pet, species.Stats.Style, enemy, random, new AttackOptions { Multiplier = e.Amount, Element = e.Element }).Total;
                                        result.PetDamage += d; threat.AddDamage(petKey, d, pet.ThreatMultiplier);
                                        break;
                                    case SkillEffectKind.DamageOverTime:
                                        var s = pet.Style(species.Stats.Style);
                                        dots.Add((CombatMath.MaxHit(s.DamageLevel, s.EffectivePower) * 0.7 * e.Amount / e.Seconds, time + e.Seconds));
                                        break;
                                    case SkillEffectKind.Heal:
                                        var amount = CombatMath.Heal(pet, random, e.Amount).Amount;
                                        if (petHealth / pet.MaxHealth < ownerHealth / owner.MaxHealth) petHealth = Math.Min(pet.MaxHealth, petHealth + amount);
                                        else result.Healed += Heal(ref ownerHealth, owner.MaxHealth, amount);
                                        threat.Add(petKey, ThreatTable<object>.HealingThreat(amount, 1));
                                        break;
                                    case SkillEffectKind.HealOverTime:
                                        var total = CombatMath.Heal(pet, random, e.Amount).Amount;
                                        heals.Add((total / e.Seconds, time + e.Seconds));
                                        petHealth = Math.Min(pet.MaxHealth, petHealth + total);
                                        threat.Add(petKey, ThreatTable<object>.HealingThreat(total, 1));
                                        break;
                                    case SkillEffectKind.Taunt:
                                        threat.Taunt(petKey, e.Seconds);
                                        break;
                                    case SkillEffectKind.DamageTaken when e.Amount < 0:
                                        hardenUntil = time + e.Seconds; harden = -e.Amount;
                                        break;
                                    case SkillEffectKind.DamageTaken:
                                        hexUntil = time + e.Seconds; hex = e.Amount;
                                        break;
                                    case SkillEffectKind.AttackSpeed when a.Targeting == SkillTargeting.Allies:
                                        ownerSpeedUntil = petSpeedUntil = time + e.Seconds; ownerSpeed = petSpeed = e.Amount;
                                        break;
                                    case SkillEffectKind.AttackSpeed:
                                        petSpeedUntil = time + e.Seconds; petSpeed = e.Amount;
                                        break;
                                    case SkillEffectKind.Stun:
                                        stunUntil = time + e.Seconds;
                                        break;
                                }
                            }
                            readyAt[skill.Id] = time + a.Cooldown;
                            petPauseUntil = time + SkillTrees.SharedPause;
                            used = true;
                            break;
                        }
                    }

                    if (!used)
                    {
                        var hit = CombatMath.Attack(pet, species.Stats.Style, enemy, random).Total;
                        result.PetDamage += hit;
                        threat.AddDamage(petKey, hit, pet.ThreatMultiplier);
                    }
                    nextPet = time + interval / (time < petSpeedUntil ? 1 + petSpeed : 1);
                }
                else
                {
                    nextEnemy = time + EnemySwing;
                    if (time < stunUntil) continue;
                    target = threat.ChooseTarget(target, false, k => k == ownerKey || PetUp(time));
                    if (target == null) target = ownerKey;
                    result.EnemyHits++;
                    if (target == petKey && PetUp(time))
                    {
                        result.HitsOnPet++;
                        pet.DamageTakenMultiplier = time < hardenUntil ? 1 - harden : 1;
                        var hit = CombatMath.Attack(enemy, AttackStyle.Melee, pet, random).Total;
                        result.PetTaken += hit;
                        petHealth -= hit;
                        if (petHealth <= 0)
                        {
                            result.Knockouts++;
                            knockedOutUntil = time + BattlePets.KnockoutSeconds;
                            threat.Remove(petKey);
                            target = ownerKey;
                        }
                    }
                    else
                    {
                        var hit = CombatMath.Attack(enemy, AttackStyle.Melee, owner, random).Total;
                        result.OwnerTaken += hit;
                        ownerHealth -= hit;
                        if (ownerHealth <= 0) { result.OwnerFalls++; ownerHealth = owner.MaxHealth; }
                    }
                }
            }
            return result;
        }

        private static double Heal(ref double health, int max, double amount)
        {
            var before = health;
            health = Math.Min(max, health + amount);
            return health - before;
        }
    }
}

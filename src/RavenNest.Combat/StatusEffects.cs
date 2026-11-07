using System;
using System.Collections.Generic;

namespace RavenNest.Combat
{
    public enum StatusEffectKind
    {
        /// <summary>Fire: damage over time, refreshed by new burns.</summary>
        Burn = 0,
        /// <summary>Ice: slower attacks per stack; five stacks freeze.</summary>
        Chill = 1,
        /// <summary>Ice at five stacks: cannot act.</summary>
        Freeze = 2,
        /// <summary>Poison: stacking damage over time that ignores armour.</summary>
        Poison = 3,
        /// <summary>Cannot act (lightning, shield bash, leap).</summary>
        Stun = 4,
        /// <summary>Physical damage over time (crits with bleed passives).</summary>
        Bleed = 5,
        /// <summary>Cannot move, but can still attack what is in reach (Snare).</summary>
        Root = 6,
    }

    public sealed class StatusEffect
    {
        public StatusEffectKind Kind;

        /// <summary>Damage per second for damage over time, per stack for poison.</summary>
        public double DamagePerSecond;

        public int Stacks = 1;
        public double Remaining;
        public double Duration;
    }

    /// <summary>
    ///     The numbers behind each element's effect (docs/combat-v2-design.md, section 2). Kept in one place so the
    ///     simulator can tune them.
    /// </summary>
    public static class ElementRules
    {
        public const double BurnSeconds = 4;
        /// <summary>A burn deals this share of the fire damage that lit it, spread over its duration.</summary>
        public const double BurnShare = 0.30;

        public const double ChillSeconds = 4;
        public const double ChillSlowPerStack = 0.20;
        public const int ChillStacksToFreeze = 5;
        public const double FreezeSeconds = 1.5;

        public const double PoisonSeconds = 6;
        public const int PoisonMaxStacks = 5;
        /// <summary>Each poison stack deals this share of the poison damage that applied it, every second.</summary>
        public const double PoisonSharePerSecond = 0.10;

        /// <summary>Lightning jumps to one more enemy nearby with this share of the blow, and stuns now and then.</summary>
        public const double ShockChainShare = 0.30;
        public const double ShockStunChance = 0.10;
        public const double ShockStunSeconds = 0.5;

        /// <summary>Bosses take crowd control at this share of its duration, then ignore it for <see cref="BossImmunitySeconds"/>.</summary>
        public const double BossCrowdControlShare = 0.5;
        public const double BossImmunitySeconds = 6;
    }

    /// <summary>
    ///     The effects on one fighter (docs/combat-v2-design.md, section 5). The game and the simulator call
    ///     <see cref="Tick"/> with the time passed and apply what it returns.
    /// </summary>
    public sealed class StatusEffectSet
    {
        private readonly List<StatusEffect> effects = new List<StatusEffect>();
        private double crowdControlImmuneFor;

        /// <summary>Bosses take shorter crowd control, then ignore it for a while.</summary>
        public bool IsBoss { get; set; }

        public IReadOnlyList<StatusEffect> Active => effects;

        /// <summary>Stunned or frozen: the fighter does nothing until it passes.</summary>
        public bool CannotAct => Has(StatusEffectKind.Stun) || Has(StatusEffectKind.Freeze);

        /// <summary>Rooted, stunned or frozen: the fighter stays where it is.</summary>
        public bool CannotMove => CannotAct || Has(StatusEffectKind.Root);

        private double crowdControlImmunity;

        /// <summary>Iron Skin: no stun, freeze or root lands for this long, and those on it end.</summary>
        public void ImmuneToCrowdControl(double seconds)
        {
            crowdControlImmunity = Math.Max(crowdControlImmunity, seconds);
            effects.RemoveAll(x => x.Kind == StatusEffectKind.Stun || x.Kind == StatusEffectKind.Freeze || x.Kind == StatusEffectKind.Root);
        }

        /// <summary>Attack intervals are divided by this (chill slows attacks).</summary>
        public double AttackSpeedFactor
        {
            get
            {
                var chill = Find(StatusEffectKind.Chill);
                return chill == null ? 1 : Math.Max(0.2, 1 - ElementRules.ChillSlowPerStack * chill.Stacks);
            }
        }

        public bool Has(StatusEffectKind kind) => Find(kind) != null;

        public StatusEffect Find(StatusEffectKind kind)
        {
            for (var i = 0; i < effects.Count; i++)
                if (effects[i].Kind == kind) return effects[i];
            return null;
        }

        /// <summary>Turns the element effects a blow triggered into status effects, and returns lightning's chained damage (for the game to deal to an enemy nearby).</summary>
        public double ApplyProcs(IEnumerable<ElementProc> procs, ICombatRandom random)
        {
            var chained = 0.0;
            foreach (var proc in procs)
            {
                switch (proc.Element)
                {
                    case Element.Fire:
                        Burn(proc.Damage * ElementRules.BurnShare / ElementRules.BurnSeconds, ElementRules.BurnSeconds);
                        break;
                    case Element.Ice:
                        Chill();
                        break;
                    case Element.Poison:
                        Poison(proc.Damage * ElementRules.PoisonSharePerSecond);
                        break;
                    case Element.Lightning:
                        chained += proc.Damage * ElementRules.ShockChainShare;
                        if (random.Next() < ElementRules.ShockStunChance)
                            CrowdControl(StatusEffectKind.Stun, ElementRules.ShockStunSeconds);
                        break;
                }
            }

            return chained;
        }

        /// <summary>A new burn replaces a weaker one and refreshes the time.</summary>
        public void Burn(double damagePerSecond, double seconds)
        {
            var burn = Find(StatusEffectKind.Burn);
            if (burn == null)
            {
                effects.Add(new StatusEffect { Kind = StatusEffectKind.Burn, DamagePerSecond = damagePerSecond, Remaining = seconds, Duration = seconds });
                return;
            }

            burn.DamagePerSecond = Math.Max(burn.DamagePerSecond, damagePerSecond);
            burn.Remaining = seconds;
        }

        public void Bleed(double damagePerSecond, double seconds)
        {
            var bleed = Find(StatusEffectKind.Bleed);
            if (bleed == null)
            {
                effects.Add(new StatusEffect { Kind = StatusEffectKind.Bleed, DamagePerSecond = damagePerSecond, Remaining = seconds, Duration = seconds });
                return;
            }

            bleed.DamagePerSecond = Math.Max(bleed.DamagePerSecond, damagePerSecond);
            bleed.Remaining = seconds;
        }

        /// <summary>One more stack of chill; the fifth turns into a freeze.</summary>
        public void Chill()
        {
            var chill = Find(StatusEffectKind.Chill);
            if (chill == null)
            {
                effects.Add(new StatusEffect { Kind = StatusEffectKind.Chill, Remaining = ElementRules.ChillSeconds, Duration = ElementRules.ChillSeconds });
                return;
            }

            chill.Remaining = ElementRules.ChillSeconds;
            if (++chill.Stacks >= ElementRules.ChillStacksToFreeze)
            {
                effects.Remove(chill);
                CrowdControl(StatusEffectKind.Freeze, ElementRules.FreezeSeconds);
            }
        }

        /// <summary>One more stack of poison, up to five; each stack keeps the strongest damage seen and the time refreshes.</summary>
        public void Poison(double damagePerSecondPerStack)
        {
            var poison = Find(StatusEffectKind.Poison);
            if (poison == null)
            {
                effects.Add(new StatusEffect { Kind = StatusEffectKind.Poison, DamagePerSecond = damagePerSecondPerStack, Remaining = ElementRules.PoisonSeconds, Duration = ElementRules.PoisonSeconds });
                return;
            }

            poison.Stacks = Math.Min(ElementRules.PoisonMaxStacks, poison.Stacks + 1);
            poison.DamagePerSecond = Math.Max(poison.DamagePerSecond, damagePerSecondPerStack);
            poison.Remaining = ElementRules.PoisonSeconds;
        }

        /// <summary>A stun or freeze; bosses take half and are then immune for a while. False when it did not land.</summary>
        public bool CrowdControl(StatusEffectKind kind, double seconds)
        {
            if (crowdControlImmunity > 0) return false;
            if (IsBoss)
            {
                if (crowdControlImmuneFor > 0) return false;
                seconds *= ElementRules.BossCrowdControlShare;
                crowdControlImmuneFor = seconds + ElementRules.BossImmunitySeconds;
            }

            var current = Find(kind);
            if (current != null)
            {
                current.Remaining = Math.Max(current.Remaining, seconds);
                return true;
            }

            effects.Add(new StatusEffect { Kind = kind, Remaining = seconds, Duration = seconds });
            return true;
        }

        /// <summary>Removes harmful effects (a Cleanse): burn, chill, poison and bleed. Stuns and freezes run out on their own.</summary>
        public void Cleanse() => effects.RemoveAll(x => x.Kind == StatusEffectKind.Burn || x.Kind == StatusEffectKind.Chill || x.Kind == StatusEffectKind.Poison || x.Kind == StatusEffectKind.Bleed);

        /// <summary>
        ///     Moves time on by <paramref name="seconds"/> and returns the damage over time taken in it: burn and bleed
        ///     (reduced by the caller's armour or resistance as they like; poison ignores armour by design).
        /// </summary>
        public ElementValues Tick(double seconds)
        {
            var damage = new ElementValues();
            if (crowdControlImmuneFor > 0) crowdControlImmuneFor = Math.Max(0, crowdControlImmuneFor - seconds);
            if (crowdControlImmunity > 0) crowdControlImmunity = Math.Max(0, crowdControlImmunity - seconds);

            for (var i = effects.Count - 1; i >= 0; i--)
            {
                var effect = effects[i];
                var time = Math.Min(seconds, effect.Remaining);
                switch (effect.Kind)
                {
                    case StatusEffectKind.Burn: damage.Fire += effect.DamagePerSecond * time; break;
                    case StatusEffectKind.Poison: damage.Poison += effect.DamagePerSecond * effect.Stacks * time; break;
                    case StatusEffectKind.Bleed: damage.Physical += effect.DamagePerSecond * time; break;
                }

                effect.Remaining -= seconds;
                if (effect.Remaining <= 0) effects.RemoveAt(i);
            }

            return damage;
        }

        public void Clear()
        {
            effects.Clear();
            crowdControlImmuneFor = 0;
        }
    }
}

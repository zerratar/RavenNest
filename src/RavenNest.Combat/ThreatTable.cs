using System;
using System.Collections.Generic;

namespace RavenNest.Combat
{
    /// <summary>
    ///     Who an enemy is angry with (docs/combat-v2-design.md, section 4). Damage adds threat, healing adds half
    ///     of what it healed spread over the enemies fighting the one healed, tank skills multiply it. An enemy only
    ///     changes target when someone clearly passes the current one, so targets do not flicker.
    /// </summary>
    public sealed class ThreatTable<TKey>
    {
        /// <summary>A melee enemy switches when someone has this much more threat than its target.</summary>
        public const double MeleeSwitchFactor = 1.10;

        /// <summary>A ranged or magic enemy is harder to pull: it switches at this much more.</summary>
        public const double RangedSwitchFactor = 1.30;

        /// <summary>Healing threat is this share of the amount healed.</summary>
        public const double HealingThreatShare = 0.5;

        private readonly Dictionary<TKey, double> threat = new Dictionary<TKey, double>();
        private TKey forcedTarget;
        private double forcedFor;

        public int Count => threat.Count;

        public double Get(TKey key) => threat.TryGetValue(key, out var value) ? value : 0;

        public void Add(TKey key, double amount)
        {
            if (amount <= 0 || key == null) return;
            threat[key] = Get(key) + amount;
        }

        /// <summary>Damage dealt to this enemy, times the attacker's threat multiplier (tanks have more).</summary>
        public void AddDamage(TKey attacker, double damage, double threatMultiplier = 1) => Add(attacker, damage * threatMultiplier);

        /// <summary>The threat one enemy gets from a heal: half the amount, shared by every enemy fighting the healed one.</summary>
        public static double HealingThreat(double healed, int enemiesFighting) =>
            healed * HealingThreatShare / Math.Max(1, enemiesFighting);

        /// <summary>A taunt: this one is the target for <paramref name="seconds"/>, and gets enough threat to keep it after.</summary>
        public void Taunt(TKey key, double seconds)
        {
            if (key == null) return;
            var top = 0.0;
            foreach (var value in threat.Values) top = Math.Max(top, value);
            threat[key] = Math.Max(Get(key), top * MeleeSwitchFactor + 1);
            forcedTarget = key;
            forcedFor = seconds;
        }

        public void Remove(TKey key)
        {
            threat.Remove(key);
            if (forcedFor > 0 && EqualityComparer<TKey>.Default.Equals(forcedTarget, key)) forcedFor = 0;
        }

        public void Clear()
        {
            threat.Clear();
            forcedFor = 0;
        }

        public void Tick(double seconds) => forcedFor = Math.Max(0, forcedFor - seconds);

        /// <summary>
        ///     The target to have now: a taunt while it lasts, otherwise the current target unless someone has clearly
        ///     more threat. <paramref name="isAlive"/> leaves out the dead and gone.
        /// </summary>
        public TKey ChooseTarget(TKey current, bool rangedEnemy, Func<TKey, bool> isAlive)
        {
            if (forcedFor > 0 && forcedTarget != null && isAlive(forcedTarget))
                return forcedTarget;

            var hasCurrent = current != null && isAlive(current) && threat.ContainsKey(current);
            var best = default(TKey);
            var bestThreat = double.MinValue;
            foreach (var pair in threat)
            {
                if (!isAlive(pair.Key)) continue;
                if (pair.Value > bestThreat)
                {
                    best = pair.Key;
                    bestThreat = pair.Value;
                }
            }

            if (!hasCurrent) return best;
            var factor = rangedEnemy ? RangedSwitchFactor : MeleeSwitchFactor;
            return bestThreat > Get(current) * factor ? best : current;
        }
    }
}

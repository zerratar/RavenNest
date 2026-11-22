using System;

namespace RavenNest.Combat
{
    /// <summary>
    ///     What a fighter's capstone path remembers between blows (the game's docs/capstone-paths-design.md): the
    ///     target it last hit and how often in a row, the count to the next Aftershock or Volley, and a block that
    ///     has not been answered yet. One per fighter, kept by the game and by the simulator.
    /// </summary>
    public sealed class PathState
    {
        /// <summary>The enemy last hit by a basic attack: another one is a new target (First Blood, Flowing Steel).</summary>
        public object Target;

        /// <summary>Hits in a row on <see cref="Target"/>, up to <see cref="PathRules.ComboStacks"/> (Flowing Steel).</summary>
        public int Row;

        /// <summary>Melee hits since the last Aftershock.</summary>
        public int MeleeHits;

        /// <summary>Shots that hit since the last Volley.</summary>
        public int Shots;

        /// <summary>Blocked a blow: the next hit is stronger (Retribution).</summary>
        public bool Retort;

        /// <summary>When the last basic attack landed, on the caller's clock in seconds (<see cref="PathRules.Rest"/>).</summary>
        public double LastHit;

        /// <summary>When Guardian Angel last saved someone, on the caller's clock; ready again <see cref="PathRules.AngelSeconds"/> later.</summary>
        public double AngelAt = double.NegativeInfinity;

        /// <summary>The target died or went away: the next enemy is a new target, also when it is the same one come back.</summary>
        public void Forget(object target)
        {
            if (target != null && !ReferenceEquals(Target, target)) return;
            Target = null;
            Row = 0;
        }
    }

    /// <summary>What a blow set off, for the game (and the simulator) to carry out.</summary>
    public struct PathBlow
    {
        /// <summary>This was the first hit on a new target with First Blood (it was a critical hit).</summary>
        public bool First;

        /// <summary>This hit answered a block (Retribution).</summary>
        public bool Retorted;

        /// <summary>Aftershock: this much damage to every other enemy around the attacker; 0 when it was not the fourth hit.</summary>
        public int Splash;

        /// <summary>Volley: this much damage to one more enemy near the target; 0 when it was not the third shot.</summary>
        public int Second;
    }

    /// <summary>
    ///     The rules of the capstone paths that need more than one blow to say (docs/capstone-paths-design.md), shared
    ///     by the game and the simulator. The ones a single blow can settle (Clean Kill, Blood Rage, Thin the Herd's
    ///     damage, poison on a crit) are in <see cref="CombatMath.Attack"/>. Only basic attacks count for a new
    ///     target, hits in a row, the fourth hit and the third shot: a skill that hits twelve enemies would otherwise
    ///     make the next swing a "first hit" again.
    /// </summary>
    public static class PathRules
    {
        /// <summary>Clean Kill: above this share of health an enemy is healthy.</summary>
        public const double HealthyThreshold = 0.8;

        /// <summary>Blood Rage: all of its power at this share of health and below.</summary>
        public const double RageFloor = 0.3;

        /// <summary>Flowing Steel: the most hits in a row that count.</summary>
        public const int ComboStacks = 5;

        /// <summary>Aftershock: every this many melee hits.</summary>
        public const int AftershockEvery = 4;

        /// <summary>Aftershock reaches this far around the attacker (Whirlwind's reach).</summary>
        public const double AftershockRadius = 4;

        /// <summary>Volley: every this many shots.</summary>
        public const int VolleyEvery = 3;

        /// <summary>Volley's second enemy is at most this far from the target (Multishot's reach).</summary>
        public const double VolleyRadius = 8;

        /// <summary>Poison on a crit lasts this long.</summary>
        public const double PoisonSeconds = 4;

        /// <summary>Thin the Herd: how long a kill's rush lasts.</summary>
        public const double RushSeconds = 6;

        /// <summary>Shield of the Many and Guardian Angel reach allies this near.</summary>
        public const double AllyRadius = 10;

        /// <summary>A shield from life steal's overflow lasts this long after the last drop into it.</summary>
        public const double OverflowShieldSeconds = 10;

        /// <summary>Wildfire: a burn jumps to an enemy at most this far from the one that died.</summary>
        public const double BurnJumpRadius = 6;

        /// <summary>
        ///     Without a hit for this long the target is forgotten: the next enemy (the same one too, come back after
        ///     it fell to someone else) is a new target, and the hits in a row start again.
        /// </summary>
        public const double RestSeconds = 6;

        /// <summary>Guardian Angel saves one ally, then not again for this long.</summary>
        public const double AngelSeconds = 60;

        /// <summary>Call before a blow with the caller's clock: forgets the target after <see cref="RestSeconds"/> without a hit.</summary>
        public static void Rest(PathState state, double now)
        {
            if (state == null) return;
            if (state.Target != null && now - state.LastHit > RestSeconds) state.Forget(null);
        }

        /// <summary>Guardian Angel: true when this fighter's angel is ready, and then it is used.</summary>
        public static bool UseAngel(PathState state, double now)
        {
            if (state == null || now - state.AngelAt < AngelSeconds) return false;
            state.AngelAt = now;
            return true;
        }

        /// <summary>Blood Rage: the share of its power at a share of health left (0 at full health, 1 at 30% and below).</summary>
        public static double Rage(double healthShare)
        {
            var missing = (1 - healthShare) / (1 - RageFloor);
            return missing < 0 ? 0 : missing > 1 ? 1 : missing;
        }

        /// <summary>Flowing Steel: how much faster the fighter attacks now (added to its attack speed).</summary>
        public static double ComboSpeed(PathState state, CombatSheet sheet) =>
            state == null || sheet == null || sheet.ComboSpeed <= 0 ? 0 : sheet.ComboSpeed * Math.Min(state.Row, ComboStacks);

        /// <summary>The same from the stat alone, for the game (its swing times do not go through a sheet).</summary>
        public static double ComboSpeed(PathState state, double perHit) =>
            state == null || perHit <= 0 ? 0 : perHit * Math.Min(state.Row, ComboStacks);

        /// <summary>
        ///     Before a blow: what the attacker's path changes about it. First Blood makes a basic attack on a new
        ///     target a critical hit that ignores part of its armor; Retribution makes the blow after a block (basic
        ///     or a skill's first) stronger.
        /// </summary>
        public static AttackOptions Before(PathState state, CombatSheet attacker, object target, AttackOptions options, bool basic)
        {
            if (state == null || attacker == null) return options;
            if (basic && attacker.FirstStrike > 0 && !ReferenceEquals(state.Target, target))
            {
                options.Critical = true;
                options.ArmorIgnored += attacker.FirstStrike;
            }
            if (state.Retort && attacker.BlockRetort > 0)
                options.Multiplier = (options.Multiplier > 0 ? options.Multiplier : 1) * (1 + attacker.BlockRetort);
            return options;
        }

        /// <summary>
        ///     After a blow: the path's memory moves on, and what the blow set off comes back. A miss changes nothing
        ///     (the first hit and the answer to a block wait for a blow that lands).
        /// </summary>
        public static PathBlow After(PathState state, CombatSheet attacker, AttackStyle style, object target, AttackResult result, bool basic, double now = 0)
        {
            var blow = new PathBlow();
            if (state == null || attacker == null || result == null || !result.Hit) return blow;

            if (state.Retort)
            {
                blow.Retorted = attacker.BlockRetort > 0;
                state.Retort = false;
            }

            if (!basic) return blow;

            state.LastHit = now;
            if (!ReferenceEquals(state.Target, target))
            {
                blow.First = attacker.FirstStrike > 0;
                state.Target = target;
                state.Row = 1;
            }
            else if (state.Row < ComboStacks) state.Row++;

            if (style == AttackStyle.Melee && attacker.Aftershock > 0 && ++state.MeleeHits >= AftershockEvery)
            {
                state.MeleeHits = 0;
                blow.Splash = Math.Max(1, (int)Math.Round(result.Total * attacker.Aftershock));
            }
            if (style == AttackStyle.Ranged && attacker.Volley > 0 && ++state.Shots >= VolleyEvery)
            {
                state.Shots = 0;
                blow.Second = Math.Max(1, (int)Math.Round(result.Total * attacker.Volley));
            }
            return blow;
        }

        /// <summary>The defender blocked a blow: with Retribution its next hit answers.</summary>
        public static void Blocked(PathState state, CombatSheet defender)
        {
            if (state != null && defender != null && defender.BlockRetort > 0) state.Retort = true;
        }

        /// <summary>
        ///     Shield of the Many: of a blow of <paramref name="damage"/> on an ally, the part the guardian takes in the
        ///     ally's place (the ally takes the rest). Never the whole blow.
        /// </summary>
        public static int GuardShare(int damage, double share)
        {
            if (damage <= 0 || share <= 0) return 0;
            return Math.Min(damage - 1, (int)Math.Round(damage * Math.Min(share, 0.5)));
        }

        /// <summary>
        ///     What the guardian loses for a share taken (<see cref="GuardShare"/>): its own armor and damage
        ///     reduction take their cut of it, as a tank pet's do of its share of its owner's blows (the blow was
        ///     worked out against the ally).
        /// </summary>
        public static int GuardTaken(int shared, CombatSheet guardian)
        {
            if (shared <= 0 || guardian == null) return Math.Max(0, shared);
            var left = (1 - CombatMath.ArmorReduction(guardian.EffectiveArmor)) * guardian.DamageTakenMultiplier;
            return Math.Max(1, (int)Math.Round(shared * left));
        }

        /// <summary>
        ///     Holy Fire: what a heal of <paramref name="healed"/> strikes an enemy for. Holy damage: armor does not
        ///     take from it, what makes the enemy take more or less does, and the undead take the healer's bonus.
        /// </summary>
        public static int HealStrike(int healed, CombatSheet healer, CombatSheet enemy)
        {
            if (healed <= 0 || healer == null || healer.HealStrike <= 0) return 0;
            var amount = healed * healer.HealStrike * healer.DamageMultiplier;
            if (enemy != null)
            {
                if (enemy.Family == EnemyFamily.Undead) amount *= 1 + healer.UndeadDamage;
                amount *= enemy.DamageTakenMultiplier;
            }
            return Math.Max(1, (int)Math.Round(amount));
        }

        /// <summary>
        ///     Crimson Pact: the shield after life steal of <paramref name="healed"/> on a fighter missing
        ///     <paramref name="missing"/> health with <paramref name="shieldNow"/> of shield left: what did not fit
        ///     into its health is added, up to the stat's share of its maximum health. A bigger shield from elsewhere
        ///     (Barrier) is left as it is.
        /// </summary>
        public static int OverflowShield(int healed, int missing, int shieldNow, int maxHealth, double share)
        {
            var over = healed - Math.Max(0, missing);
            if (over <= 0 || share <= 0 || maxHealth <= 0) return shieldNow;
            var cap = (int)Math.Round(maxHealth * share);
            if (shieldNow >= cap) return shieldNow;
            return Math.Min(cap, Math.Max(0, shieldNow) + over);
        }

        /// <summary>Guardian Angel: the health the saved ally is healed for (it was left at 1).</summary>
        public static int AngelHeal(int maxHealth, double share) => share <= 0 ? 0 : Math.Max(1, (int)Math.Round(maxHealth * Math.Min(share, 1)));

        /// <summary>Wildfire: the burn that arrives on the next enemy (a second for what was left of the time).</summary>
        public static double BurnJump(double damagePerSecond, double share) => share <= 0 ? 0 : damagePerSecond * Math.Min(share, 1);
    }
}

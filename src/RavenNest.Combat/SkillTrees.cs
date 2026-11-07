using System;
using System.Collections.Generic;
using System.Linq;

namespace RavenNest.Combat
{
    /// <summary>The eight trees, one per combat skill (the game's docs/combat-skills-design.md, section 2).</summary>
    public enum SkillTree
    {
        Bulwark,      // Defense: the tank
        Restoration,  // Healing: the healer
        Might,        // Strength: big melee hits
        Precision,    // Attack: accuracy and crits
        Marksman,     // Ranged
        Elements,     // Magic: the caster
        Vitality,     // Health: staying alive, for everyone
        Hunter,       // Slayer: bosses and big enemies
    }

    /// <summary>The combat skills whose levels open the trees' tiers.</summary>
    public enum CombatSkill { Attack, Defense, Strength, Health, Ranged, Magic, Healing, Slayer }

    /// <summary>When the character uses an active by itself (section 3); the loadout's order is the priority.</summary>
    public enum SkillUse
    {
        /// <summary>Whenever it is ready and there is something to fight.</summary>
        InFight,
        /// <summary>Three or more enemies near.</summary>
        EnemiesNear,
        /// <summary>The target is a boss (or an elite enemy).</summary>
        OnBoss,
        /// <summary>An ally (or yourself) is below 60% health.</summary>
        AllyHurt,
        /// <summary>You are below half health.</summary>
        SelfHurt,
        /// <summary>The target is below 20% health.</summary>
        TargetLow,
        /// <summary>An enemy near is attacking someone else.</summary>
        EnemyOnAlly,
        /// <summary>An ally has a harmful effect (burn, poison, chill, bleed).</summary>
        AllyHarmed,
    }

    /// <summary>Who an active reaches.</summary>
    public enum SkillTargeting
    {
        Target,          // the enemy fought
        Self,
        LowestAlly,      // the ally (or yourself) with the least health left
        Allies,          // allies near, up to MaxTargets
        Cone,            // enemies in front, up to MaxTargets
        Around,          // enemies around you
        AroundTarget,    // enemies around the target
        Chain,           // the target, then jumps to MaxTargets - 1 more
        FarthestEnemy,   // an enemy at range (Leap)
    }

    public enum SkillEffectKind
    {
        Damage,              // Amount: times a basic hit
        DamageOverTime,      // Amount: times a basic hit, spread over Seconds
        Heal,                // Amount: times a basic heal
        HealOverTime,        // Amount: times a basic heal, spread over Seconds
        HealPercent,         // Amount: share of the target's max health
        Shield,              // Amount: times a basic heal, absorbed before health, for Seconds
        DamageTaken,         // Amount: share more (positive) or less (negative) damage taken, for Seconds
        DamageDealt,         // Amount: share more or less damage dealt, for Seconds
        AttackSpeed,         // Amount: share faster attacks, for Seconds
        Taunt,               // enemies hit attack you, for Seconds
        Stun,                // Seconds
        Root,                // Seconds
        Freeze,              // Seconds
        Cleanse,             // removes burn, chill, poison and bleed
        CrowdControlImmunity // Seconds
    }

    /// <summary>One thing an active does.</summary>
    public sealed class SkillEffect
    {
        public SkillEffectKind Kind;
        public double Amount;
        public double Seconds;
        public Element? Element;
        public double ArmorIgnored;
        public int Hits = 1;

        public SkillEffect(SkillEffectKind kind, double amount = 0, double seconds = 0)
        {
            Kind = kind;
            Amount = amount;
            Seconds = seconds;
        }
    }

    /// <summary>What an active needs worn to be used.</summary>
    public enum SkillGear { None, Shield, MeleeWeapon, Bow, Staff }

    /// <summary>An active skill: used automatically on its cooldown when its use rule says so.</summary>
    public sealed class ActiveSkill
    {
        public double Cooldown;
        public SkillTargeting Targeting;
        public int MaxTargets = 1;
        public double Radius;
        public SkillUse Use;
        public SkillGear Needs;
        public AttackStyle Style = AttackStyle.Melee;
        public SkillEffect[] Effects = Array.Empty<SkillEffect>();
    }

    /// <summary>A node of a tree: an active, a passive with ranks, or a special one (Battle Rhythm, Last Stand).</summary>
    public sealed class SkillNode
    {
        public string Id;
        public string Name;
        public string Description;
        public SkillTree Tree;

        /// <summary>0 to 3: opens at the tree's skill level 1, 100, 300 and 600.</summary>
        public int Tier;
        public int MaxRank = 1;

        /// <summary>Points per rank.</summary>
        public int Cost = 1;

        /// <summary>A passive's stats per rank, in GearStats' text form ("CRIT_CHANCE:1%").</summary>
        public string Passive;

        public ActiveSkill Active;

        /// <summary>Battle Rhythm: one more loadout slot.</summary>
        public bool ExtraLoadoutSlot;

        /// <summary>Last Stand: once per fight, survive a killing blow with 1 health.</summary>
        public bool LastStand;

        public bool IsActive => Active != null;
        public int RequiredLevel => SkillTrees.TierLevels[Tier];
    }

    /// <summary>
    ///     The skill trees (docs/combat-skills-design.md): eight trees of actives and passives, one pool of points from
    ///     combat levels, nodes opened by the tree's skill level, a loadout of four actives (five with Battle Rhythm).
    ///     The numbers are a starting point; the combat simulator tunes them.
    /// </summary>
    public static class SkillTrees
    {
        public static readonly int[] TierLevels = { 1, 100, 300, 600 };

        /// <summary>
        ///     Raised whenever a balance update changes the trees (a node's numbers, its cost, one removed): everyone who
        ///     had spent points then gets a free respec, so nobody is stuck with a build the update broke.
        /// </summary>
        public const int Version = 1;
        public const int BaseLoadoutSlots = 4;
        public const int RespecCostPerPoint = 1000;

        /// <summary>The pause between two actives of one character, so it does not fire everything at once.</summary>
        public const double SharedPause = 1.5;

        public static CombatSkill SkillOf(SkillTree tree)
        {
            switch (tree)
            {
                case SkillTree.Bulwark: return CombatSkill.Defense;
                case SkillTree.Restoration: return CombatSkill.Healing;
                case SkillTree.Might: return CombatSkill.Strength;
                case SkillTree.Precision: return CombatSkill.Attack;
                case SkillTree.Marksman: return CombatSkill.Ranged;
                case SkillTree.Elements: return CombatSkill.Magic;
                case SkillTree.Vitality: return CombatSkill.Health;
                default: return CombatSkill.Slayer;
            }
        }

        /// <summary>
        ///     Points from the eight combat skills' levels added up: one per 10 levels up to 1,000, then one per 40. Every
        ///     combat skill at 999 gives 274; the trees cost far more, so nobody fills them all.
        /// </summary>
        public static int EarnedPoints(int totalCombatLevels)
        {
            if (totalCombatLevels <= 0) return 0;
            var early = Math.Min(totalCombatLevels, 1000) / 10;
            var late = Math.Max(0, totalCombatLevels - 1000) / 40;
            return early + late;
        }

        public static int RespecCost(int pointsSpent) => Math.Max(0, pointsSpent) * RespecCostPerPoint;

        private static readonly Dictionary<string, SkillNode> byId = new Dictionary<string, SkillNode>(StringComparer.OrdinalIgnoreCase);
        private static readonly List<SkillNode> all = new List<SkillNode>();

        public static IReadOnlyList<SkillNode> All => all;

        public static SkillNode Get(string id) => id != null && byId.TryGetValue(id.Trim(), out var node) ? node : null;

        /// <summary>A node by id or by name ("cleave", "Crushing Blow"), for chat commands.</summary>
        public static SkillNode Find(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            var key = text.Trim();
            return Get(key) ?? Get(key.Replace(' ', '-')) ?? all.FirstOrDefault(n => n.Name.Equals(key, StringComparison.OrdinalIgnoreCase))
                ?? all.FirstOrDefault(n => n.Name.StartsWith(key, StringComparison.OrdinalIgnoreCase));
        }

        public static IEnumerable<SkillNode> Of(SkillTree tree) => all.Where(n => n.Tree == tree);

        /// <summary>What every node of every tree costs at full rank.</summary>
        public static int TotalCost => all.Sum(n => n.MaxRank * n.Cost);

        // ---- the trees ---------------------------------------------------------------------------------------

        /// <summary>Points per rank of a passive by tier (deeper is dearer), and an active's price by tier.</summary>
        private static readonly int[] PassiveCost = { 1, 2, 3, 4 };
        private static readonly int[] ActiveCost = { 3, 5, 8, 10 };

        private static SkillNode Passive(SkillTree tree, int tier, string id, string name, string description, string perRank, int ranks = 5, int cost = 0) =>
            Add(new SkillNode { Tree = tree, Tier = tier, Id = id, Name = name, Description = description, Passive = perRank, MaxRank = ranks, Cost = cost > 0 ? cost : PassiveCost[tier] });

        private static SkillNode Active(SkillTree tree, int tier, string id, string name, string description, ActiveSkill active, int cost = 0) =>
            Add(new SkillNode { Tree = tree, Tier = tier, Id = id, Name = name, Description = description, Active = active, Cost = cost > 0 ? cost : ActiveCost[tier] });

        private static SkillNode Add(SkillNode node)
        {
            all.Add(node);
            byId[node.Id] = node;
            return node;
        }

        private static SkillEffect E(SkillEffectKind kind, double amount = 0, double seconds = 0) => new SkillEffect(kind, amount, seconds);

        static SkillTrees()
        {
            const SkillTree D = SkillTree.Bulwark, H = SkillTree.Restoration, S = SkillTree.Might, A = SkillTree.Precision;
            const SkillTree R = SkillTree.Marksman, M = SkillTree.Elements, V = SkillTree.Vitality, L = SkillTree.Hunter;

            // Defense: Bulwark, the tank
            Passive(D, 0, "thick-skin", "Thick Skin", "More armour.", "ARMOR_BONUS:4%");
            Passive(D, 0, "watchful", "Watchful", "Enemies pay you more attention.", "THREAT:8%");
            Active(D, 0, "taunt", "Taunt", "Enemies near you attack you for 4 seconds.",
                new ActiveSkill { Cooldown = 12, Targeting = SkillTargeting.Around, Radius = 6, MaxTargets = 8, Use = SkillUse.EnemyOnAlly, Effects = new[] { E(SkillEffectKind.Taunt, 0, 4) } });
            Active(D, 1, "shield-wall", "Shield Wall", "Take 40% less damage for 6 seconds.",
                new ActiveSkill { Cooldown = 30, Targeting = SkillTargeting.Self, Use = SkillUse.SelfHurt, Effects = new[] { E(SkillEffectKind.DamageTaken, -0.4, 6) } });
            Passive(D, 1, "shield-training", "Shield Training", "A chance to block half a blow (with a shield).", "BLOCK_CHANCE:3%");
            Passive(D, 1, "steadfast", "Steadfast", "Less damage from everything.", "DAMAGE_REDUCTION:1%");
            Active(D, 2, "shield-bash", "Shield Bash", "Stuns the target for 1.5 seconds. Needs a shield.",
                new ActiveSkill { Cooldown = 15, Targeting = SkillTargeting.Target, Use = SkillUse.InFight, Needs = SkillGear.Shield, Effects = new[] { E(SkillEffectKind.Damage, 0.5), E(SkillEffectKind.Stun, 0, 1.5) } });
            Passive(D, 2, "spiked-armour", "Spiked Armour", "Melee attackers hurt themselves on you.", "THORNS:3%");
            Passive(D, 2, "hold-the-line", "Hold the Line", "Less damage while below half health.", "LOW_HEALTH_REDUCTION:5%");
            Active(D, 3, "rally", "Rally", "Allies near you take 15% less damage for 8 seconds.",
                new ActiveSkill { Cooldown = 40, Targeting = SkillTargeting.Allies, Radius = 10, MaxTargets = 10, Use = SkillUse.OnBoss, Effects = new[] { E(SkillEffectKind.DamageTaken, -0.15, 8) } });
            Add(new SkillNode { Tree = D, Tier = 3, Id = "last-stand", Name = "Last Stand", Description = "Once per fight, a killing blow leaves you at 1 health instead.", LastStand = true, Cost = 10 });

            // Healing: Restoration, the healer
            Active(H, 0, "mend", "Mend", "A big heal on the ally with the least health.",
                new ActiveSkill { Cooldown = 10, Targeting = SkillTargeting.LowestAlly, Radius = 12, Use = SkillUse.AllyHurt, Style = AttackStyle.Healing, Effects = new[] { E(SkillEffectKind.Heal, 2.5) } });
            Passive(H, 0, "devotion", "Devotion", "Stronger heals.", "HEALING_POWER_BONUS:5%");
            Active(H, 1, "renew", "Renew", "Heals up to three allies over 8 seconds.",
                new ActiveSkill { Cooldown = 20, Targeting = SkillTargeting.Allies, Radius = 12, MaxTargets = 3, Use = SkillUse.AllyHurt, Style = AttackStyle.Healing, Effects = new[] { E(SkillEffectKind.HealOverTime, 2.0, 8) } });
            Passive(H, 1, "quick-hands", "Quick Hands", "Faster heals and spells.", "CAST_SPEED:3%");
            Passive(H, 1, "gentle-presence", "Gentle Presence", "Enemies mind your healing less.", "THREAT:-8%");
            Active(H, 2, "barrier", "Barrier", "A shield on the ally with the least health that takes damage for 10 seconds.",
                new ActiveSkill { Cooldown = 25, Targeting = SkillTargeting.LowestAlly, Radius = 12, Use = SkillUse.AllyHurt, Style = AttackStyle.Healing, Effects = new[] { E(SkillEffectKind.Shield, 2.0, 10) } });
            Active(H, 2, "cleanse", "Cleanse", "Removes burns, poison, chills and bleeding from an ally.",
                new ActiveSkill { Cooldown = 15, Targeting = SkillTargeting.LowestAlly, Radius = 12, Use = SkillUse.AllyHarmed, Style = AttackStyle.Healing, Effects = new[] { E(SkillEffectKind.Cleanse) } });
            Passive(H, 2, "warding-light", "Warding Light", "Your heals also give a small shield.", "HEAL_SHIELD:5%");
            Active(H, 3, "sanctuary", "Sanctuary", "Heals everyone near you over 6 seconds.",
                new ActiveSkill { Cooldown = 45, Targeting = SkillTargeting.Allies, Radius = 10, MaxTargets = 20, Use = SkillUse.AllyHurt, Style = AttackStyle.Healing, Effects = new[] { E(SkillEffectKind.HealOverTime, 3.0, 6) } });
            Passive(H, 3, "smite", "Smite", "More damage to the undead.", "UNDEAD_DAMAGE:10%", ranks: 3);

            // Strength: Might, big melee hits
            Passive(S, 0, "brute-force", "Brute Force", "More melee power.", "MELEE_POWER_BONUS:4%");
            Active(S, 0, "cleave", "Cleave", "Hits up to three enemies in front of you.",
                new ActiveSkill { Cooldown = 8, Targeting = SkillTargeting.Cone, Radius = 4, MaxTargets = 3, Use = SkillUse.InFight, Needs = SkillGear.MeleeWeapon, Effects = new[] { E(SkillEffectKind.Damage, 0.8) } });
            Active(S, 1, "crushing-blow", "Crushing Blow", "A slow, huge hit.",
                new ActiveSkill { Cooldown = 18, Targeting = SkillTargeting.Target, Use = SkillUse.InFight, Needs = SkillGear.MeleeWeapon, Effects = new[] { E(SkillEffectKind.Damage, 3.0) } });
            Passive(S, 1, "rend", "Rend", "Critical hits make the target bleed.", "BLEED_ON_CRIT:6%");
            Active(S, 2, "whirlwind", "Whirlwind", "Hits every enemy around you.",
                new ActiveSkill { Cooldown = 14, Targeting = SkillTargeting.Around, Radius = 4, MaxTargets = 12, Use = SkillUse.EnemiesNear, Needs = SkillGear.MeleeWeapon, Effects = new[] { E(SkillEffectKind.Damage, 1.0) } });
            Passive(S, 2, "executioner", "Executioner", "More damage to enemies below 30% health.", "EXECUTE_DAMAGE:6%");
            Active(S, 3, "leap", "Leap", "Leaps at an enemy further off and stuns it briefly.",
                new ActiveSkill { Cooldown = 20, Targeting = SkillTargeting.FarthestEnemy, Radius = 15, Use = SkillUse.InFight, Needs = SkillGear.MeleeWeapon, Effects = new[] { E(SkillEffectKind.Damage, 1.5), E(SkillEffectKind.Stun, 0, 0.75) } });
            Passive(S, 3, "titan", "Titan", "More melee power and health.", "MELEE_POWER_BONUS:10%;MAX_HEALTH_BONUS:5%", ranks: 1, cost: 10);

            // Attack: Precision, accuracy and crits
            Passive(A, 0, "keen-eye", "Keen Eye", "More accuracy.", "ACCURACY_BONUS:4%");
            Passive(A, 0, "sharp-edge", "Sharp Edge", "More critical hits.", "CRIT_CHANCE:1%");
            Active(A, 1, "expose-weakness", "Expose Weakness", "The target takes 15% more damage from everyone for 6 seconds.",
                new ActiveSkill { Cooldown = 20, Targeting = SkillTargeting.Target, Use = SkillUse.OnBoss, Effects = new[] { E(SkillEffectKind.DamageTaken, 0.15, 6) } });
            Passive(A, 1, "deadly", "Deadly", "Critical hits hit harder.", "CRIT_DAMAGE:5%");
            Active(A, 2, "flurry", "Flurry", "Three quick hits.",
                new ActiveSkill { Cooldown = 12, Targeting = SkillTargeting.Target, Use = SkillUse.InFight, Needs = SkillGear.MeleeWeapon, Effects = new[] { new SkillEffect(SkillEffectKind.Damage, 0.6) { Hits = 3 } } });
            Passive(A, 2, "swift", "Swift", "Faster attacks.", "ATTACK_SPEED:2%");
            Active(A, 3, "disarm", "Disarm", "The target hits 30% softer for 5 seconds.",
                new ActiveSkill { Cooldown = 25, Targeting = SkillTargeting.Target, Use = SkillUse.OnBoss, Effects = new[] { E(SkillEffectKind.DamageDealt, -0.3, 5) } });
            Passive(A, 3, "precision-strikes", "Precision Strikes", "Your blows ignore part of the target's armour.", "ARMOR_PENETRATION:15%", ranks: 1, cost: 10);

            // Ranged: Marksman
            Passive(R, 0, "steady-aim", "Steady Aim", "More ranged power.", "RANGED_POWER_BONUS:4%");
            Active(R, 0, "multishot", "Multishot", "Shoots up to three enemies.",
                new ActiveSkill { Cooldown = 10, Targeting = SkillTargeting.AroundTarget, Radius = 8, MaxTargets = 3, Use = SkillUse.InFight, Needs = SkillGear.Bow, Style = AttackStyle.Ranged, Effects = new[] { E(SkillEffectKind.Damage, 0.7) } });
            Active(R, 1, "piercing-shot", "Piercing Shot", "A shot that ignores half the target's armour.",
                new ActiveSkill { Cooldown = 12, Targeting = SkillTargeting.Target, Use = SkillUse.InFight, Needs = SkillGear.Bow, Style = AttackStyle.Ranged, Effects = new[] { new SkillEffect(SkillEffectKind.Damage, 1.5) { ArmorIgnored = 0.5 } } });
            Passive(R, 1, "eagle-eye", "Eagle Eye", "More critical hits.", "CRIT_CHANCE:1%");
            Active(R, 2, "arrow-rain", "Arrow Rain", "Arrows fall on the enemies around the target for 4 seconds.",
                new ActiveSkill { Cooldown = 25, Targeting = SkillTargeting.AroundTarget, Radius = 5, MaxTargets = 12, Use = SkillUse.EnemiesNear, Needs = SkillGear.Bow, Style = AttackStyle.Ranged, Effects = new[] { E(SkillEffectKind.DamageOverTime, 2.0, 4) } });
            Passive(R, 2, "hunters-pace", "Hunter's Pace", "Faster attacks.", "ATTACK_SPEED:2%");
            Active(R, 3, "snare", "Snare", "Roots the enemies around the target for 3 seconds.",
                new ActiveSkill { Cooldown = 30, Targeting = SkillTargeting.AroundTarget, Radius = 5, MaxTargets = 8, Use = SkillUse.EnemiesNear, Needs = SkillGear.Bow, Style = AttackStyle.Ranged, Effects = new[] { E(SkillEffectKind.Root, 0, 3) } });
            Active(R, 3, "rapid-fire", "Rapid Fire", "Twice the attack speed for 5 seconds.",
                new ActiveSkill { Cooldown = 40, Targeting = SkillTargeting.Self, Use = SkillUse.OnBoss, Needs = SkillGear.Bow, Style = AttackStyle.Ranged, Effects = new[] { E(SkillEffectKind.AttackSpeed, 1.0, 5) } });

            // Magic: Elements, the caster
            Passive(M, 0, "arcane-power", "Arcane Power", "More magic power.", "MAGIC_POWER_BONUS:4%");
            Active(M, 0, "fireball", "Fireball", "Fire on the target and the enemies around it.",
                new ActiveSkill { Cooldown = 8, Targeting = SkillTargeting.AroundTarget, Radius = 3, MaxTargets = 5, Use = SkillUse.InFight, Needs = SkillGear.Staff, Style = AttackStyle.Magic, Effects = new[] { new SkillEffect(SkillEffectKind.Damage, 1.2) { Element = Combat.Element.Fire } } });
            Active(M, 1, "frost-nova", "Frost Nova", "Freezes the enemies around you for 2 seconds.",
                new ActiveSkill { Cooldown = 20, Targeting = SkillTargeting.Around, Radius = 5, MaxTargets = 10, Use = SkillUse.EnemiesNear, Needs = SkillGear.Staff, Style = AttackStyle.Magic, Effects = new[] { new SkillEffect(SkillEffectKind.Damage, 0.5) { Element = Combat.Element.Ice }, E(SkillEffectKind.Freeze, 0, 2) } });
            Passive(M, 1, "quickened-casting", "Quickened Casting", "Faster spells and heals.", "CAST_SPEED:3%");
            Passive(M, 1, "searing", "Searing", "Your burns hurt more.", "BURN_BONUS:10%");
            Active(M, 2, "chain-lightning", "Chain Lightning", "Lightning that jumps to four enemies.",
                new ActiveSkill { Cooldown = 14, Targeting = SkillTargeting.Chain, Radius = 8, MaxTargets = 5, Use = SkillUse.InFight, Needs = SkillGear.Staff, Style = AttackStyle.Magic, Effects = new[] { new SkillEffect(SkillEffectKind.Damage, 1.0) { Element = Combat.Element.Lightning } } });
            Active(M, 2, "toxic-cloud", "Toxic Cloud", "Poison on the enemies around the target for 6 seconds.",
                new ActiveSkill { Cooldown = 20, Targeting = SkillTargeting.AroundTarget, Radius = 4, MaxTargets = 10, Use = SkillUse.EnemiesNear, Needs = SkillGear.Staff, Style = AttackStyle.Magic, Effects = new[] { new SkillEffect(SkillEffectKind.DamageOverTime, 2.0, 6) { Element = Combat.Element.Poison } } });
            Passive(M, 2, "unravel", "Unravel", "The enemies you hit resist elements less.", "RESISTANCE_SHRED:3%");
            Active(M, 3, "meteor", "Meteor", "A huge fireball on the target and everything around it.",
                new ActiveSkill { Cooldown = 60, Targeting = SkillTargeting.AroundTarget, Radius = 5, MaxTargets = 12, Use = SkillUse.OnBoss, Needs = SkillGear.Staff, Style = AttackStyle.Magic, Effects = new[] { new SkillEffect(SkillEffectKind.Damage, 4.0) { Element = Combat.Element.Fire } } });

            // Health: Vitality, staying alive, for everyone
            Passive(V, 0, "hardy", "Hardy", "More health.", "MAX_HEALTH_BONUS:3%");
            Active(V, 0, "second-wind", "Second Wind", "Heals you for a quarter of your health.",
                new ActiveSkill { Cooldown = 45, Targeting = SkillTargeting.Self, Use = SkillUse.SelfHurt, Effects = new[] { E(SkillEffectKind.HealPercent, 0.25) } });
            Passive(V, 1, "regeneration", "Regeneration", "Regain health in combat.", "COMBAT_REGEN:0.2%");
            Passive(V, 1, "bloodthirst", "Bloodthirst", "Your hits heal you a little.", "LIFE_STEAL:1%");
            Add(new SkillNode { Tree = V, Tier = 2, Id = "battle-rhythm", Name = "Battle Rhythm", Description = "One more active in your loadout.", ExtraLoadoutSlot = true, Cost = 8 });
            Passive(V, 2, "resilience", "Resilience", "More healing received.", "HEALING_RECEIVED:4%");
            Active(V, 3, "iron-skin", "Iron Skin", "Nothing can stun, freeze or root you for 4 seconds.",
                new ActiveSkill { Cooldown = 40, Targeting = SkillTargeting.Self, Use = SkillUse.OnBoss, Effects = new[] { E(SkillEffectKind.CrowdControlImmunity, 0, 4) } });

            // Slayer: Hunter, bosses and big enemies
            Passive(L, 0, "big-game", "Big Game", "More damage to bosses.", "BOSS_DAMAGE:4%");
            Active(L, 0, "mark-prey", "Mark Prey", "The target takes 20% more damage from your group for 8 seconds.",
                new ActiveSkill { Cooldown = 30, Targeting = SkillTargeting.Target, Use = SkillUse.OnBoss, Effects = new[] { E(SkillEffectKind.DamageTaken, 0.2, 8) } });
            Active(L, 1, "execute", "Execute", "A huge hit on an enemy below 20% health.",
                new ActiveSkill { Cooldown = 15, Targeting = SkillTargeting.Target, Use = SkillUse.TargetLow, Effects = new[] { E(SkillEffectKind.Damage, 4.0) } });
            Passive(L, 1, "treasure-hunter", "Treasure Hunter", "More loot in raids and dungeons.", "LOOT_LUCK:2%");
            Passive(L, 2, "pressure-points", "Pressure Points", "Your stuns, freezes and roots last longer.", "CROWD_CONTROL_DURATION:10%");
            Passive(L, 3, "apex-predator", "Apex Predator", "Much more damage to bosses.", "BOSS_DAMAGE:10%", ranks: 1, cost: 10);
        }
    }

    /// <summary>
    ///     A character's choices: ranks in nodes and the loadout, as text the server stores
    ///     ("cleave:1;brute-force:3|cleave,taunt"). Checks what may be raised and slotted.
    /// </summary>
    public sealed class SkillBuild
    {
        public readonly Dictionary<string, int> Ranks = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        public readonly List<string> Loadout = new List<string>();

        public int Rank(string id) => id != null && Ranks.TryGetValue(id, out var rank) ? rank : 0;
        public bool Has(string id) => Rank(id) > 0;

        public int Spent => Ranks.Sum(r => (SkillTrees.Get(r.Key)?.Cost ?? 0) * r.Value);

        /// <summary>When each active is used (by node id), where the player chose other than its default (the website).</summary>
        public readonly Dictionary<string, SkillUse> Rules = new Dictionary<string, SkillUse>(StringComparer.OrdinalIgnoreCase);

        /// <summary>The use rule for an active: the player's choice, or the skill's default.</summary>
        public SkillUse RuleOf(SkillNode node) => node?.Active == null ? SkillUse.InFight : Rules.TryGetValue(node.Id, out var rule) ? rule : node.Active.Use;

        /// <summary>Sets when an active is used; its default clears the choice.</summary>
        public string SetRule(string id, SkillUse rule)
        {
            var node = SkillTrees.Get(id);
            if (node == null || !node.IsActive) return "That is not an active skill.";
            if (rule == node.Active.Use) Rules.Remove(node.Id); else Rules[node.Id] = rule;
            return null;
        }

        public int LoadoutSlots => SkillTrees.BaseLoadoutSlots + Ranks.Count(r => r.Value > 0 && (SkillTrees.Get(r.Key)?.ExtraLoadoutSlot ?? false));

        public bool HasLastStand => Ranks.Any(r => r.Value > 0 && (SkillTrees.Get(r.Key)?.LastStand ?? false));

        /// <summary>The passives' stats, ranks counted.</summary>
        public GearStats Passives()
        {
            var total = new GearStats();
            foreach (var (id, rank) in Ranks)
            {
                var node = SkillTrees.Get(id);
                if (node?.Passive == null || rank <= 0) continue;
                total.Add(GearStats.Parse(node.Passive), Math.Min(rank, node.MaxRank));
            }
            return total;
        }

        /// <summary>The loadout's actives, in order (the priority).</summary>
        public IEnumerable<SkillNode> Actives() => Loadout.Select(SkillTrees.Get).Where(n => n != null && n.IsActive && Has(n.Id));

        /// <summary>Null when the node can take one more rank, otherwise why not (for chat).</summary>
        public string CanRaise(string id, Func<CombatSkill, int> levelOf, int earnedPoints)
        {
            var node = SkillTrees.Get(id);
            if (node == null) return "There is no such skill.";
            if (Rank(id) >= node.MaxRank) return node.Name + " is already at its highest rank.";
            var level = levelOf(SkillTrees.SkillOf(node.Tree));
            if (level < node.RequiredLevel) return node.Name + " needs " + SkillTrees.SkillOf(node.Tree) + " " + node.RequiredLevel + ".";
            var left = earnedPoints - Spent;
            if (left < node.Cost) return node.Name + " costs " + node.Cost + " point" + (node.Cost == 1 ? "" : "s") + "; you have " + Math.Max(0, left) + ".";
            return null;
        }

        public string Raise(string id, Func<CombatSkill, int> levelOf, int earnedPoints)
        {
            var reason = CanRaise(id, levelOf, earnedPoints);
            if (reason != null) return reason;
            var node = SkillTrees.Get(id);
            Ranks[node.Id] = Rank(node.Id) + 1;
            return null;
        }

        /// <summary>One rank off a node (the website's planner); out of the loadout when none is left.</summary>
        public void Lower(string id)
        {
            var node = SkillTrees.Get(id);
            if (node == null || Rank(node.Id) <= 0) return;
            var rank = Rank(node.Id) - 1;
            if (rank > 0) { Ranks[node.Id] = rank; return; }
            Ranks.Remove(node.Id);
            Rules.Remove(node.Id);
            for (var i = 0; i < Loadout.Count; ++i)
                if (string.Equals(Loadout[i], node.Id, StringComparison.OrdinalIgnoreCase)) Loadout[i] = "";
            Trim();
        }

        /// <summary>Puts an active in a slot (0 based); null when done, otherwise why not.</summary>
        public string SetLoadout(int slot, string id)
        {
            if (slot < 0 || slot >= LoadoutSlots) return "Slots go from 1 to " + LoadoutSlots + ".";
            var node = SkillTrees.Get(id);
            if (node == null) return "There is no such skill.";
            if (!node.IsActive) return node.Name + " is a passive; it always works.";
            if (!Has(node.Id)) return "You have not learned " + node.Name + ".";
            Loadout.RemoveAll(x => x.Equals(node.Id, StringComparison.OrdinalIgnoreCase));
            while (Loadout.Count < slot) Loadout.Add("");
            if (slot < Loadout.Count) Loadout[slot] = node.Id; else Loadout.Add(node.Id);
            Trim();
            return null;
        }

        public void ClearSlot(int slot)
        {
            if (slot >= 0 && slot < Loadout.Count) Loadout[slot] = "";
            Trim();
        }

        private void Trim()
        {
            while (Loadout.Count > 0 && string.IsNullOrEmpty(Loadout[Loadout.Count - 1])) Loadout.RemoveAt(Loadout.Count - 1);
            while (Loadout.Count > LoadoutSlots) Loadout.RemoveAt(Loadout.Count - 1);
        }

        /// <summary>Every point back and the loadout emptied.</summary>
        public void Reset()
        {
            Ranks.Clear();
            Loadout.Clear();
            Rules.Clear();
        }

        /// <summary>Drops what is no longer allowed (a node removed in a balance update, ranks above the maximum, an emptied loadout slot).</summary>
        public void Clean()
        {
            foreach (var id in Ranks.Keys.ToList())
            {
                var node = SkillTrees.Get(id);
                if (node == null || Ranks[id] <= 0) Ranks.Remove(id);
                else if (Ranks[id] > node.MaxRank) Ranks[id] = node.MaxRank;
            }
            for (var i = 0; i < Loadout.Count; ++i)
            {
                var node = SkillTrees.Get(Loadout[i]);
                if (node == null || !node.IsActive || !Has(node.Id)) Loadout[i] = "";
            }
            Trim();
        }

        public static SkillBuild Parse(string text)
        {
            var build = new SkillBuild();
            if (string.IsNullOrWhiteSpace(text)) return build;
            var halves = text.Split('|');
            foreach (var part in halves[0].Split(';'))
            {
                var kv = part.Split(':');
                if (kv.Length != 2 || !int.TryParse(kv[1], out var rank) || rank <= 0) continue;
                build.Ranks[kv[0].Trim()] = rank;
            }
            if (halves.Length > 1)
            {
                foreach (var id in halves[1].Split(',')) build.Loadout.Add(id.Trim());
            }
            if (halves.Length > 2)
            {
                foreach (var part in halves[2].Split(','))
                {
                    var kv = part.Split(':');
                    if (kv.Length == 2 && Enum.TryParse<SkillUse>(kv[1].Trim(), true, out var rule)) build.Rules[kv[0].Trim()] = rule;
                }
            }
            build.Clean();
            return build;
        }

        public override string ToString()
        {
            var text = string.Join(";", Ranks.Where(r => r.Value > 0).OrderBy(r => r.Key).Select(r => r.Key + ":" + r.Value)) + "|" + string.Join(",", Loadout);
            var rules = Rules.Where(r => Has(r.Key)).OrderBy(r => r.Key).Select(r => r.Key + ":" + r.Value).ToList();
            return rules.Count == 0 ? text : text + "|" + string.Join(",", rules);
        }
    }
}

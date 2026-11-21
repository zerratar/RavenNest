using System;
using System.Collections.Generic;
using System.Linq;

namespace RavenNest.Combat
{
    /// <summary>
    ///     The trees: one per combat skill (the game's docs/combat-skills-design.md, section 2), and Beastmaster for
    ///     Taming ("Beastmaster", the same document): points in the pet instead of in yourself.
    /// </summary>
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
        Beastmaster,  // Taming: the battle pet
    }

    /// <summary>
    ///     The skills whose levels open the trees' tiers: the eight combat skills, and Taming for Beastmaster. Taming
    ///     is no combat skill (it stays out of the combat level); it earns points of its own (SkillTrees.TamingPoints).
    /// </summary>
    public enum CombatSkill { Attack, Defense, Strength, Health, Ranged, Magic, Healing, Slayer, Taming }

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
        /// <summary>Your battle pet is below half health, or knocked out.</summary>
        PetHurt,
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
        Pet,             // your battle pet: damage is its own blow on the target, the rest lands on the pet itself
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

    /// <summary>The numbers of skills in words (SkillNode.EffectText).</summary>
    public static class SkillTexts
    {
        private static string Pct(double v) => (v * 100).ToString(Math.Abs(v * 100) < 10 && Math.Abs(v * 100 - Math.Round(v * 100)) > 0.001 ? "0.#" : "0", System.Globalization.CultureInfo.InvariantCulture) + "%";
        private static string Num(double v) => v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

        // a stat in plain words: {0} is the amount; "less" ones turn round when negative
        private static readonly Dictionary<GearStat, (string More, string Less)> Phrases = new Dictionary<GearStat, (string, string)>
        {
            [GearStat.ArmorBonus] = ("{0} more armor", "{0} less armor"),
            [GearStat.Threat] = ("{0} more threat", "{0} less threat"),
            [GearStat.BlockChance] = ("{0} chance to block half a blow", null),
            [GearStat.DamageReduction] = ("{0} less damage taken", null),
            [GearStat.Thorns] = ("{0} of melee damage taken hurts the attacker", null),
            [GearStat.LowHealthReduction] = ("{0} less damage taken below half health", null),
            [GearStat.HealingPowerBonus] = ("{0} stronger heals", null),
            [GearStat.CastSpeed] = ("{0} faster heals and spells", null),
            [GearStat.HealShield] = ("{0} of each heal also as a shield", null),
            [GearStat.UndeadDamage] = ("{0} more damage to the undead", null),
            [GearStat.MeleePowerBonus] = ("{0} more melee damage", null),
            [GearStat.RangedPowerBonus] = ("{0} more ranged damage", null),
            [GearStat.MagicPowerBonus] = ("{0} more magic damage", null),
            [GearStat.BleedOnCrit] = ("crits bleed {0} of the hit over 4 s", null),
            [GearStat.ExecuteDamage] = ("{0} more damage to enemies below 30% health", null),
            [GearStat.MaxHealthBonus] = ("{0} more health", null),
            [GearStat.AccuracyBonus] = ("{0} more accuracy", null),
            [GearStat.CritChance] = ("{0} crit chance", null),
            [GearStat.CritDamage] = ("{0} crit damage", null),
            [GearStat.AttackSpeed] = ("{0} faster attacks", null),
            [GearStat.ArmorPenetration] = ("{0} of the target's armor ignored", null),
            [GearStat.BurnBonus] = ("{0} stronger burns", null),
            [GearStat.ResistanceShred] = ("{0} of the target's resistances ignored", null),
            [GearStat.CombatRegen] = ("{0} of your health back each second in fights", null),
            [GearStat.LifeSteal] = ("{0} of the damage you deal heals you", null),
            [GearStat.HealingReceived] = ("{0} more healing received", null),
            [GearStat.BossDamage] = ("{0} more damage to bosses", null),
            [GearStat.LootLuck] = ("{0} more loot in raids and dungeons", null),
            [GearStat.GemKeepChance] = ("{0} better chance to keep a gem you take out", null),
            [GearStat.CrowdControlDuration] = ("{0} longer stuns, freezes and roots", null),
            [GearStat.PetPower] = ("your pet's blows and heals are {0} stronger", null),
            [GearStat.PetHealth] = ("{0} more health for your pet", null),
            [GearStat.PetRecovery] = ("your pet gets up {0} sooner after a knockout", null),
            [GearStat.PetGathering] = ("your pet's gathering skills are {0} stronger", null),
            [GearStat.PetGuard] = ("a tank pet takes {0} more of the blows meant for you", null),
        };

        /// <summary>Only the amounts ("5%", or "10% and 5%" for two stats), for totals beside the full words.</summary>
        public static string Amounts(GearStats stats, int ranks)
        {
            var parts = new List<string>();
            for (var i = 0; i < GearStats.Count; ++i)
            {
                var v = stats.Values[i] * ranks;
                if (v != 0) parts.Add(GearStats.IsShare((GearStat)i) ? Pct(Math.Abs(v)) : Num(Math.Abs(v)));
            }
            return string.Join(" and ", parts);
        }

        public static string Stats(GearStats stats, int ranks)
        {
            var parts = new List<string>();
            for (var i = 0; i < GearStats.Count; ++i)
            {
                var v = stats.Values[i] * ranks;
                if (v == 0) continue;
                var stat = (GearStat)i;
                var amount = GearStats.IsShare(stat) ? Pct(Math.Abs(v)) : Num(Math.Abs(v));
                if (Phrases.TryGetValue(stat, out var p) && (v > 0 || p.Less != null))
                    parts.Add(string.Format(v > 0 ? p.More : p.Less, amount));
                else
                    parts.Add((v > 0 ? "+" : "-") + amount + " " + GearStats.Label(stat));
            }
            return string.Join(", ", parts);
        }

        public static string Active(ActiveSkill a)
        {
            var parts = new List<string>();
            foreach (var e in a.Effects) parts.Add(a.Targeting == SkillTargeting.Pet ? PetEffect(e) : Effect(e));
            var who = Targets(a);
            var text = string.Join(", ", parts.Where(p => p.Length > 0));
            return who.Length > 0 ? text + " " + who : text;
        }

        private static string Effect(SkillEffect e)
        {
            var element = e.Element.HasValue ? " " + e.Element.Value.ToString().ToLowerInvariant() : "";
            switch (e.Kind)
            {
                case SkillEffectKind.Damage:
                    var hit = e.Hits > 1 ? e.Hits + " hits of " + Pct(e.Amount) + " of a hit" : Pct(e.Amount) + " of a hit as" + (element.Length > 0 ? element : "") + " damage";
                    if (e.Hits > 1 && element.Length > 0) hit += " (" + element.Trim() + ")";
                    if (e.Hits <= 1 && element.Length == 0) hit = Pct(e.Amount) + " of a hit";
                    return e.ArmorIgnored > 0 ? hit + ", ignoring " + Pct(e.ArmorIgnored) + " of armor" : hit;
                case SkillEffectKind.DamageOverTime: return Pct(e.Amount) + " of a hit" + (element.Length > 0 ? " as" + element + " damage" : "") + " over " + Num(e.Seconds) + " s";
                case SkillEffectKind.Heal: return "a heal of " + Pct(e.Amount) + " of a normal heal";
                case SkillEffectKind.HealOverTime: return Pct(e.Amount) + " of a normal heal over " + Num(e.Seconds) + " s";
                case SkillEffectKind.HealPercent: return "heals " + Pct(e.Amount) + " of maximum health";
                case SkillEffectKind.Shield: return "a shield of " + Pct(e.Amount) + " of a heal for " + Num(e.Seconds) + " s";
                case SkillEffectKind.DamageTaken: return (e.Amount > 0 ? "+" : "") + Pct(e.Amount) + " damage taken for " + Num(e.Seconds) + " s";
                case SkillEffectKind.DamageDealt: return (e.Amount > 0 ? "+" : "") + Pct(e.Amount) + " damage dealt for " + Num(e.Seconds) + " s";
                case SkillEffectKind.AttackSpeed: return "+" + Pct(e.Amount) + " attack speed for " + Num(e.Seconds) + " s";
                case SkillEffectKind.Taunt: return "taunted for " + Num(e.Seconds) + " s";
                case SkillEffectKind.Stun: return "stunned for " + Num(e.Seconds) + " s";
                case SkillEffectKind.Root: return "rooted for " + Num(e.Seconds) + " s";
                case SkillEffectKind.Freeze: return "frozen for " + Num(e.Seconds) + " s";
                case SkillEffectKind.Cleanse: return "burns, poison, chills and bleeding removed";
                case SkillEffectKind.CrowdControlImmunity: return "immune to stuns, freezes and roots for " + Num(e.Seconds) + " s";
                default: return "";
            }
        }

        // a command for the pet: its own blow on the target, and what it gains itself
        private static string PetEffect(SkillEffect e)
        {
            switch (e.Kind)
            {
                case SkillEffectKind.Damage: return "your pet strikes the target for " + Pct(e.Amount) + " of its hit";
                case SkillEffectKind.HealPercent: return "your pet gets " + Pct(e.Amount) + " of its health back (and back up, if it was knocked out)";
                case SkillEffectKind.AttackSpeed: return "your pet attacks " + Pct(e.Amount) + " faster for " + Num(e.Seconds) + " s";
                case SkillEffectKind.DamageDealt: return "your pet deals " + Pct(e.Amount) + " more damage for " + Num(e.Seconds) + " s";
                case SkillEffectKind.DamageTaken: return "your pet takes " + Pct(Math.Abs(e.Amount)) + (e.Amount < 0 ? " less" : " more") + " damage for " + Num(e.Seconds) + " s";
                default: return Effect(e);
            }
        }

        private static string Targets(ActiveSkill a)
        {
            var r = a.Radius > 0 ? " within " + Num(a.Radius) + " m" : "";
            switch (a.Targeting)
            {
                case SkillTargeting.Self: return "(you)";
                case SkillTargeting.LowestAlly: return "(the ally with the least health" + r + ")";
                case SkillTargeting.Allies: return a.MaxTargets <= 1 ? "(an ally" + r + ")" : "(up to " + a.MaxTargets + " allies" + r + ")";
                case SkillTargeting.Cone: return a.MaxTargets <= 1 ? "(an enemy in front)" : "(up to " + a.MaxTargets + " enemies in front)";
                case SkillTargeting.Around: return a.MaxTargets <= 1 ? "(an enemy" + r + ")" : "(up to " + a.MaxTargets + " enemies" + r + ")";
                case SkillTargeting.AroundTarget: return "(the target and up to " + Math.Max(0, a.MaxTargets - 1) + " enemies" + r + " of it)";
                case SkillTargeting.Chain: return "(jumps to " + Math.Max(0, a.MaxTargets - 1) + " more enemies)";
                case SkillTargeting.FarthestEnemy: return "(an enemy up to " + Num(a.Radius) + " m away)";
                case SkillTargeting.Pet: return "";
                default: return "(the target)";
            }
        }
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
    public enum SkillGear
    {
        None, Shield, MeleeWeapon, Bow, Staff,
        /// <summary>A battle pet out and on its feet (Beastmaster's commands).</summary>
        Pet,
    }

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

        /// <summary>0 to 4: opens at the tree's skill level 1, 100, 300, 600 and 999 (the capstone).</summary>
        public int Tier;

        /// <summary>The tree's last node, for a maxed skill (level 999): one rank, a big passive.</summary>
        public bool Capstone;
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

        // ---- Beastmaster (the battle pet) ----

        /// <summary>Kinship: the passive's stats count only while a battle pet is out and on its feet.</summary>
        public bool WhilePetOut;

        /// <summary>
        ///     Elemental Bond: while a battle pet is out, this share of each of your hits a rank is added as the pet's
        ///     element; a pet without an element adds half of it as crit chance instead.
        /// </summary>
        public double PetElementShare;

        /// <summary>Trick Training: one more skill slot for the battle pet.</summary>
        public bool ExtraPetSkillSlot;

        /// <summary>Shared Mending: healing you receive heals your pet by the same share, and what quickens or shields you does the same for it.</summary>
        public bool HealsReachPet;

        /// <summary>Pack Leader: a packmate of the pet's own kind fights beside it at half strength.</summary>
        public bool PetTwin;

        /// <summary>
        ///     The path: one of these (the nodes linked to it in the tier below) must be learned first. Empty on the first
        ///     tier. The map draws these links.
        /// </summary>
        public string[] Requires = Array.Empty<string>();

        public bool IsActive => Active != null;
        public int RequiredLevel => SkillTrees.TierLevels[Tier];

        /// <summary>
        ///     What the node does in numbers, for the website and chat: a passive's stats a rank ("+1% Damage Reduction a
        ///     rank"), an active's effects ("80% of a hit to up to 3 enemies within 4 m"). Empty for the specials, whose
        ///     description says it all.
        /// </summary>
        public string EffectText()
        {
            if (Active != null) return SkillTexts.Active(Active);
            if (PetElementShare > 0)
                return (PetElementShare * 100).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "% of each of your hits more, as your pet's element (half as much crit chance with a pet that has none)" + (MaxRank > 1 ? ", a rank" : "");
            if (string.IsNullOrEmpty(Passive)) return "";
            var text = SkillTexts.Stats(GearStats.Parse(Passive), 1);
            if (WhilePetOut) text += " while a battle pet is out";
            return MaxRank > 1 ? text + " a rank" : text;
        }

        /// <summary>A ranked passive's total now and at its last rank ("2% now, 5% at 5/5"); empty otherwise.</summary>
        public string TotalText(int rank)
        {
            if (Active != null || string.IsNullOrEmpty(Passive) || MaxRank <= 1) return "";
            var stats = GearStats.Parse(Passive);
            return (rank > 0 ? SkillTexts.Amounts(stats, rank) + " now, " : "") + SkillTexts.Amounts(stats, MaxRank) + " at " + MaxRank + "/" + MaxRank;
        }
    }

    /// <summary>
    ///     The skill trees (docs/combat-skills-design.md): eight trees of actives and passives for the combat skills and
    ///     Beastmaster for Taming, one pool of points from combat levels and Taming's, nodes opened by the tree's skill
    ///     level, a loadout of four actives (five with Battle Rhythm).
    ///     The numbers are a starting point; the combat simulator tunes them.
    /// </summary>
    public static class SkillTrees
    {
        public static readonly int[] TierLevels = { 1, 100, 300, 600, 999 };

        /// <summary>What a capstone costs: more than anything else in a tree.</summary>
        public const int CapstoneCost = 15;

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
                case SkillTree.Beastmaster: return CombatSkill.Taming;
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

        /// <summary>
        ///     The points Taming's levels add (Beastmaster, 2026-11-21): what one combat skill's levels are worth, one
        ///     per 10 levels for the first 125 (an eighth of the first 1,000) and one per 40 after. Taming 999 gives 34.
        ///     Counted apart from the combat levels, so nobody's points from those change.
        /// </summary>
        public static int TamingPoints(int tamingLevel)
        {
            if (tamingLevel <= 0) return 0;
            return (int)Math.Floor(Math.Min(tamingLevel, 125) / 10.0 + Math.Max(0, tamingLevel - 125) / 40.0);
        }

        /// <summary>Every point a character has earned: the combat levels' and Taming's.</summary>
        public static int EarnedPoints(int totalCombatLevels, int tamingLevel) => EarnedPoints(totalCombatLevels) + TamingPoints(tamingLevel);

        /// <summary>The number of skills that open trees: a planner's list of levels, in <see cref="CombatSkill"/> order.</summary>
        public const int TreeSkills = (int)CombatSkill.Taming + 1;

        /// <summary>
        ///     The points for a planner's levels, in <see cref="CombatSkill"/> order: the eight combat skills, then
        ///     Taming. A list from before Beastmaster (eight levels, in an old shared link) counts Taming as 0.
        /// </summary>
        public static int EarnedPoints(int[] levels)
        {
            if (levels == null) return 0;
            var combat = 0;
            for (var i = 0; i < levels.Length && i < (int)CombatSkill.Taming; ++i) combat += levels[i];
            return EarnedPoints(combat, levels.Length > (int)CombatSkill.Taming ? levels[(int)CombatSkill.Taming] : 0);
        }

        public static int RespecCost(int pointsSpent) => Math.Max(0, pointsSpent) * RespecCostPerPoint;

        private static readonly Dictionary<string, SkillNode> byId = new Dictionary<string, SkillNode>(StringComparer.OrdinalIgnoreCase);
        private static readonly List<SkillNode> all = new List<SkillNode>();

        public static IReadOnlyList<SkillNode> All => all;

        public static SkillNode Get(string id) => id != null && byId.TryGetValue(id.Trim(), out var node) ? node : null;

        /// <summary>
        /// A node by id or by name ("cleave", "Crushing Blow"), for chat commands. A part of a name only when it can
        /// mean one skill: "shield" used to pick the first of Shield Wall, Shield Training and Shield Bash, and
        /// !learn spent the points on it (getting them back costs coins). Null when unknown or unclear:
        /// <see cref="NotFoundText"/> says which.
        /// </summary>
        public static SkillNode Find(string text)
        {
            var matches = Matches(text);
            return matches.Count == 1 ? matches[0] : null;
        }

        /// <summary>Every node the text could mean: the one with that id or name, otherwise those whose name starts with it, otherwise those with it as a word.</summary>
        public static IReadOnlyList<SkillNode> Matches(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return Array.Empty<SkillNode>();
            var key = text.Trim();
            var exact = Get(key) ?? Get(key.Replace(' ', '-')) ?? all.FirstOrDefault(n => n.Name.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (exact != null) return new[] { exact };
            var starts = all.Where(n => n.Name.StartsWith(key, StringComparison.OrdinalIgnoreCase)).ToList();
            if (starts.Count > 0) return starts;
            return all.Where(n => n.Name.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
        }

        /// <summary>Why <see cref="Find"/> found nothing, for chat: the skills it could mean, or that there is none.</summary>
        public static string NotFoundText(string text)
        {
            var matches = Matches(text);
            if (matches.Count > 1)
            {
                var names = matches.Take(6).Select(n => n.Name).ToList();
                var list = names.Count == 2 ? names[0] + " or " + names[1] : string.Join(", ", names.Take(names.Count - 1)) + " or " + names[names.Count - 1];
                return (text ?? "").Trim() + " could mean " + list + ". Type the full name.";
            }
            return "There is no skill called " + (text ?? "").Trim() + ". !tree Might (or another tree) lists the skills.";
        }

        /// <summary>A combat skill's name as players see it: Health is shown as Vitality since 0.9.7.</summary>
        public static string SkillName(CombatSkill skill) => skill == CombatSkill.Health ? "Vitality" : skill.ToString();

        /// <summary>When an active is used, for chat and the website ("on bosses").</summary>
        public static string UseText(SkillUse use)
        {
            switch (use)
            {
                case SkillUse.EnemiesNear: return "when three or more enemies are near";
                case SkillUse.OnBoss: return "on bosses";
                case SkillUse.AllyHurt: return "when an ally is below 60% health";
                case SkillUse.SelfHurt: return "when you are below half health";
                case SkillUse.TargetLow: return "when the target is below 20% health";
                case SkillUse.EnemyOnAlly: return "when an enemy attacks someone else";
                case SkillUse.AllyHarmed: return "when an ally burns, bleeds or is poisoned";
                case SkillUse.PetHurt: return "when your pet is below half health or knocked out";
                default: return "in any fight";
            }
        }

        /// <summary>What an active needs equipped ("a bow"), or empty: without it the skill is never used.</summary>
        public static string NeedsText(SkillGear gear)
        {
            switch (gear)
            {
                case SkillGear.Shield: return "a shield";
                case SkillGear.MeleeWeapon: return "a melee weapon";
                case SkillGear.Bow: return "a bow";
                case SkillGear.Staff: return "a staff";
                case SkillGear.Pet: return "a battle pet out";
                default: return "";
            }
        }

        public static IEnumerable<SkillNode> Of(SkillTree tree) => all.Where(n => n.Tree == tree);

        /// <summary>What every node of every tree costs at full rank.</summary>
        public static int TotalCost => all.Sum(n => n.MaxRank * n.Cost);

        // ---- the trees ---------------------------------------------------------------------------------------

        /// <summary>Points per rank of a passive by tier (deeper is dearer), and an active's price by tier.</summary>
        private static readonly int[] PassiveCost = { 1, 2, 3, 4, 5 };
        private static readonly int[] ActiveCost = { 3, 5, 8, 10, 12 };

        // A tree's capstone (2026-11-20): nothing opened at level 999, so maxing a skill gave its tree nothing. One
        // rank, for a maxed skill, from stats the trees already use.
        private static SkillNode Capstone(SkillTree tree, string id, string name, string description, string stats, params string[] requires)
        {
            var node = Add(new SkillNode { Tree = tree, Tier = 4, Id = id, Name = name, Description = description, Passive = stats, MaxRank = 1, Cost = CapstoneCost, Capstone = true });
            node.Requires = requires;
            return node;
        }

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
            Passive(D, 0, "thick-skin", "Thick Skin", "More armor.", "ARMOR_BONUS:4%");
            Passive(D, 0, "watchful", "Watchful", "Enemies pay you more attention.", "THREAT:8%");
            Active(D, 0, "taunt", "Taunt", "Enemies near you attack you for 4 seconds.",
                new ActiveSkill { Cooldown = 12, Targeting = SkillTargeting.Around, Radius = 6, MaxTargets = 8, Use = SkillUse.EnemyOnAlly, Effects = new[] { E(SkillEffectKind.Taunt, 0, 4) } });
            Active(D, 1, "shield-wall", "Shield Wall", "Take 40% less damage for 6 seconds.",
                new ActiveSkill { Cooldown = 30, Targeting = SkillTargeting.Self, Use = SkillUse.SelfHurt, Effects = new[] { E(SkillEffectKind.DamageTaken, -0.4, 6) } });
            Passive(D, 1, "shield-training", "Shield Training", "A chance to block half a blow (with a shield).", "BLOCK_CHANCE:3%");
            Passive(D, 1, "steadfast", "Steadfast", "Less damage from everything.", "DAMAGE_REDUCTION:1%");
            Active(D, 2, "shield-bash", "Shield Bash", "Stuns the target for 1.5 seconds. Needs a shield.",
                new ActiveSkill { Cooldown = 15, Targeting = SkillTargeting.Target, Use = SkillUse.InFight, Needs = SkillGear.Shield, Effects = new[] { E(SkillEffectKind.Damage, 0.5), E(SkillEffectKind.Stun, 0, 1.5) } });
            Passive(D, 2, "spiked-armour", "Spiked Armor", "Melee attackers hurt themselves on you.", "THORNS:3%");
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
            Passive(A, 3, "precision-strikes", "Precision Strikes", "Your blows ignore part of the target's armor.", "ARMOR_PENETRATION:15%", ranks: 1, cost: 10);

            // Ranged: Marksman
            Passive(R, 0, "steady-aim", "Steady Aim", "More ranged power.", "RANGED_POWER_BONUS:4%");
            Active(R, 0, "multishot", "Multishot", "Shoots up to three enemies.",
                new ActiveSkill { Cooldown = 10, Targeting = SkillTargeting.AroundTarget, Radius = 8, MaxTargets = 3, Use = SkillUse.InFight, Needs = SkillGear.Bow, Style = AttackStyle.Ranged, Effects = new[] { E(SkillEffectKind.Damage, 0.7) } });
            Active(R, 1, "piercing-shot", "Piercing Shot", "A shot that ignores half the target's armor.",
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
                new ActiveSkill { Cooldown = 8, Targeting = SkillTargeting.AroundTarget, Radius = 3, MaxTargets = 5, Use = SkillUse.InFight, Needs = SkillGear.Staff, Style = AttackStyle.Magic, Effects = new[] { new SkillEffect(SkillEffectKind.Damage, 1.0) { Element = Combat.Element.Fire } } });
            Active(M, 1, "frost-nova", "Frost Nova", "Freezes the enemies around you for 2 seconds.",
                new ActiveSkill { Cooldown = 20, Targeting = SkillTargeting.Around, Radius = 5, MaxTargets = 10, Use = SkillUse.EnemiesNear, Needs = SkillGear.Staff, Style = AttackStyle.Magic, Effects = new[] { new SkillEffect(SkillEffectKind.Damage, 0.5) { Element = Combat.Element.Ice }, E(SkillEffectKind.Freeze, 0, 2) } });
            Passive(M, 1, "quickened-casting", "Quickened Casting", "Faster spells and heals.", "CAST_SPEED:3%");
            Passive(M, 1, "searing", "Searing", "Your burns hurt more.", "BURN_BONUS:10%");
            Active(M, 2, "chain-lightning", "Chain Lightning", "Lightning that jumps to four enemies.",
                new ActiveSkill { Cooldown = 14, Targeting = SkillTargeting.Chain, Radius = 8, MaxTargets = 5, Use = SkillUse.InFight, Needs = SkillGear.Staff, Style = AttackStyle.Magic, Effects = new[] { new SkillEffect(SkillEffectKind.Damage, 0.8) { Element = Combat.Element.Lightning } } });
            Active(M, 2, "toxic-cloud", "Toxic Cloud", "Poison on the enemies around the target for 6 seconds.",
                new ActiveSkill { Cooldown = 20, Targeting = SkillTargeting.AroundTarget, Radius = 4, MaxTargets = 10, Use = SkillUse.EnemiesNear, Needs = SkillGear.Staff, Style = AttackStyle.Magic, Effects = new[] { new SkillEffect(SkillEffectKind.DamageOverTime, 2.0, 6) { Element = Combat.Element.Poison } } });
            Passive(M, 2, "unravel", "Unravel", "The enemies you hit resist elements less.", "RESISTANCE_SHRED:3%");
            Active(M, 3, "meteor", "Meteor", "A huge fireball on the target and everything around it.",
                new ActiveSkill { Cooldown = 60, Targeting = SkillTargeting.AroundTarget, Radius = 5, MaxTargets = 12, Use = SkillUse.OnBoss, Needs = SkillGear.Staff, Style = AttackStyle.Magic, Effects = new[] { new SkillEffect(SkillEffectKind.Damage, 2.8) { Element = Combat.Element.Fire } } });

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
            Passive(L, 0, "big-game", "Big Game", "More damage to bosses.", "BOSS_DAMAGE:3%");
            Active(L, 0, "mark-prey", "Mark Prey", "The target takes 20% more damage from your group for 8 seconds.",
                new ActiveSkill { Cooldown = 30, Targeting = SkillTargeting.Target, Use = SkillUse.OnBoss, Effects = new[] { E(SkillEffectKind.DamageTaken, 0.2, 8) } });
            Active(L, 1, "execute", "Execute", "A huge hit on an enemy below 20% health.",
                new ActiveSkill { Cooldown = 15, Targeting = SkillTargeting.Target, Use = SkillUse.TargetLow, Effects = new[] { E(SkillEffectKind.Damage, 3.0) } });
            Passive(L, 1, "treasure-hunter", "Treasure Hunter", "More loot in raids and dungeons.", "LOOT_LUCK:2%");
            Passive(L, 2, "gem-salvager", "Gem Salvager", "A better chance to keep a gem you take out of its socket.", "GEM_KEEP_CHANCE:5%", ranks: 3);
            Passive(L, 2, "pressure-points", "Pressure Points", "Your stuns, freezes and roots last longer.", "CROWD_CONTROL_DURATION:10%");
            Passive(L, 3, "apex-predator", "Apex Predator", "Much more damage to bosses.", "BOSS_DAMAGE:8%", ranks: 1, cost: 10);

            // Taming: Beastmaster, the battle pet (2026-11-21). Points in the pet instead of in yourself: every role of
            // pet has something here (a tank's guard, a healer's and a damage pet's power, a gatherer's skills).
            const SkillTree B = SkillTree.Beastmaster;
            Passive(B, 0, "fang-and-claw", "Fang and Claw", "Your pet hits harder and heals more.", "PET_POWER:4%");
            Passive(B, 0, "thick-hide", "Thick Hide", "More health for your pet.", "PET_HEALTH:5%");
            Active(B, 0, "sic-em", "Sic 'Em", "Your pet goes for your target at once with a heavy blow.",
                new ActiveSkill { Cooldown = 12, Targeting = SkillTargeting.Pet, Use = SkillUse.InFight, Needs = SkillGear.Pet, Effects = new[] { E(SkillEffectKind.Damage, 2.5) } });
            Passive(B, 1, "back-on-its-feet", "Back on Its Feet", "A knocked out pet gets up sooner.", "PET_RECOVERY:10%");
            Add(new SkillNode { Tree = B, Tier = 1, Id = "kinship", Name = "Kinship", Description = "You are tougher with your pet at your side.", Passive = "MAX_HEALTH_BONUS:2%", WhilePetOut = true, MaxRank = 5, Cost = PassiveCost[1] });
            Active(B, 1, "mend-pet", "Mend Pet", "Patches your pet up, and gets it back on its feet when it is knocked out.",
                new ActiveSkill { Cooldown = 30, Targeting = SkillTargeting.Pet, Use = SkillUse.PetHurt, Effects = new[] { E(SkillEffectKind.HealPercent, 0.3) } });
            Add(new SkillNode { Tree = B, Tier = 2, Id = "trick-training", Name = "Trick Training", Description = "One more skill slot for your pet.", ExtraPetSkillSlot = true, Cost = 8 });
            Add(new SkillNode { Tree = B, Tier = 2, Id = "shared-mending", Name = "Shared Mending", Description = "Healing you receive heals your pet by the same share of its health, and what makes you attack faster or take less damage does the same for it.", HealsReachPet = true, Cost = 8 });
            Passive(B, 2, "keen-senses", "Keen Senses", "A gatherer pet finds more.", "PET_GATHERING:10%", ranks: 3);
            Add(new SkillNode { Tree = B, Tier = 3, Id = "elemental-bond", Name = "Elemental Bond", Description = "With your pet out, your own hits carry its element.", PetElementShare = 0.03, MaxRank = 5, Cost = PassiveCost[3] });
            Passive(B, 3, "guardians-oath", "Guardian's Oath", "A tank pet takes more of the blows meant for you.", "PET_GUARD:20%", ranks: 1, cost: 10);
            Active(B, 3, "call-of-the-wild", "Call of the Wild", "Your pet attacks half again as fast and hits harder for 8 seconds.",
                new ActiveSkill { Cooldown = 40, Targeting = SkillTargeting.Pet, Use = SkillUse.OnBoss, Needs = SkillGear.Pet, Effects = new[] { E(SkillEffectKind.AttackSpeed, 0.5, 8), E(SkillEffectKind.DamageDealt, 0.3, 8) } });

            // the paths (2026-11-08): a node needs one of the nodes linked to it in the tier below; the first tier is open
            Path("shield-wall", "taunt", "thick-skin");
            Path("shield-training", "thick-skin");
            Path("steadfast", "watchful", "thick-skin");
            Path("shield-bash", "shield-training", "shield-wall");
            Path("spiked-armour", "steadfast", "shield-training");
            Path("hold-the-line", "steadfast");
            Path("rally", "hold-the-line", "shield-bash");
            Path("last-stand", "spiked-armour", "hold-the-line");
            Path("renew", "mend");
            Path("quick-hands", "mend", "devotion");
            Path("gentle-presence", "devotion");
            Path("barrier", "renew");
            Path("cleanse", "renew", "quick-hands");
            Path("warding-light", "gentle-presence", "quick-hands");
            Path("sanctuary", "barrier", "cleanse");
            Path("smite", "warding-light");
            Path("crushing-blow", "cleave", "brute-force");
            Path("rend", "brute-force");
            Path("whirlwind", "crushing-blow");
            Path("executioner", "rend", "crushing-blow");
            Path("leap", "whirlwind");
            Path("titan", "executioner");
            Path("expose-weakness", "keen-eye");
            Path("deadly", "sharp-edge", "keen-eye");
            Path("flurry", "expose-weakness", "deadly");
            Path("swift", "deadly");
            Path("disarm", "flurry");
            Path("precision-strikes", "swift", "flurry");
            Path("piercing-shot", "steady-aim", "multishot");
            Path("eagle-eye", "steady-aim");
            Path("arrow-rain", "piercing-shot");
            Path("hunters-pace", "eagle-eye");
            Path("snare", "arrow-rain", "hunters-pace");
            Path("rapid-fire", "hunters-pace");
            Path("frost-nova", "fireball");
            Path("quickened-casting", "arcane-power");
            Path("searing", "fireball", "arcane-power");
            Path("chain-lightning", "frost-nova", "quickened-casting");
            Path("toxic-cloud", "searing");
            Path("unravel", "quickened-casting");
            Path("meteor", "toxic-cloud", "chain-lightning");
            Path("regeneration", "hardy");
            Path("bloodthirst", "second-wind", "hardy");
            Path("battle-rhythm", "regeneration", "bloodthirst");
            Path("resilience", "bloodthirst");
            Path("iron-skin", "battle-rhythm", "resilience");
            Path("execute", "mark-prey");
            Path("treasure-hunter", "big-game");
            Path("pressure-points", "execute", "treasure-hunter");
            Path("gem-salvager", "treasure-hunter");
            Path("apex-predator", "pressure-points");
            Path("back-on-its-feet", "thick-hide");
            Path("kinship", "thick-hide", "fang-and-claw");
            Path("mend-pet", "sic-em", "fang-and-claw");
            Path("trick-training", "mend-pet", "kinship");
            Path("shared-mending", "mend-pet", "back-on-its-feet");
            Path("keen-senses", "kinship", "back-on-its-feet");
            Path("elemental-bond", "trick-training", "keen-senses");
            Path("guardians-oath", "shared-mending", "keen-senses");
            Path("call-of-the-wild", "trick-training", "shared-mending");

            Capstones();
        }

        private static void Path(string id, params string[] requires) => byId[id].Requires = requires;

        private static void Capstones()
        {
            Capstone(SkillTree.Bulwark, "unbreakable", "Unbreakable", "Nothing gets through, and no enemy looks away.", "DAMAGE_REDUCTION:6%;THREAT:25%", "rally", "last-stand");
            Capstone(SkillTree.Restoration, "miracle-worker", "Miracle Worker", "Your heals do what should not be possible.", "HEALING_POWER_BONUS:15%;HEAL_SHIELD:10%", "sanctuary", "smite");
            Capstone(SkillTree.Might, "colossus", "Colossus", "Every blow lands like a falling tree.", "MELEE_POWER_BONUS:12%;EXECUTE_DAMAGE:10%", "leap", "titan");
            Capstone(SkillTree.Precision, "perfect-form", "Perfect Form", "No wasted movement: more critical hits, and they hit much harder.", "CRIT_CHANCE:4%;CRIT_DAMAGE:20%", "disarm", "precision-strikes");
            Capstone(SkillTree.Marksman, "deadeye", "Deadeye", "You do not miss, and armor does not help them.", "RANGED_POWER_BONUS:12%;ARMOR_PENETRATION:15%", "snare", "rapid-fire");
            Capstone(SkillTree.Elements, "archmage", "Archmage", "The elements answer faster and harder.", "MAGIC_POWER_BONUS:12%;CAST_SPEED:8%", "meteor");
            Capstone(SkillTree.Vitality, "undying", "Undying", "Much more health, and it keeps coming back.", "MAX_HEALTH_BONUS:10%;COMBAT_REGEN:0.6%", "iron-skin");
            Capstone(SkillTree.Hunter, "legend-of-the-hunt", "Legend of the Hunt", "Bosses fear you, and their hoards know it.", "BOSS_DAMAGE:10%;LOOT_LUCK:5%", "apex-predator");
            // Beastmaster's is no pair of stats: a second animal at the owner's side (the same kind as the pet that is
            // out; a second pet from the stable is the proposal in the design document)
            Capstone(SkillTree.Beastmaster, "pack-leader", "Pack Leader", "A packmate of your pet's own kind fights beside it at half strength.", null, "elemental-bond", "guardians-oath", "call-of-the-wild").PetTwin = true;
        }

        /// <summary>The nodes whose path goes through this one (the tier above).</summary>
        public static IEnumerable<SkillNode> Dependents(string id) => all.Where(n => n.Requires.Any(r => string.Equals(r, id, StringComparison.OrdinalIgnoreCase)));
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

        // ---- Beastmaster: what the build does for the battle pet ----

        /// <summary>Trick Training: skill slots for the pet on top of BattlePets.SkillSlots.</summary>
        public int ExtraPetSkillSlots => Ranks.Count(r => r.Value > 0 && (SkillTrees.Get(r.Key)?.ExtraPetSkillSlot ?? false));

        public bool HealsReachPet => Ranks.Any(r => r.Value > 0 && (SkillTrees.Get(r.Key)?.HealsReachPet ?? false));

        public bool HasPetTwin => Ranks.Any(r => r.Value > 0 && (SkillTrees.Get(r.Key)?.PetTwin ?? false));

        /// <summary>Whether the owner's own stats depend on a pet being out (Kinship, Elemental Bond): they are worked out again when it comes, goes, falls or gets up.</summary>
        public bool FollowsPet => Ranks.Any(r => r.Value > 0 && SkillTrees.Get(r.Key) is SkillNode n && (n.WhilePetOut || n.PetElementShare > 0));

        /// <summary>Elemental Bond: the share of each of the owner's hits added as the pet's element, ranks counted.</summary>
        public double PetElementShare()
        {
            var total = 0d;
            foreach (var r in Ranks)
            {
                var node = SkillTrees.Get(r.Key);
                if (node != null && node.PetElementShare > 0 && r.Value > 0) total += node.PetElementShare * Math.Min(r.Value, node.MaxRank);
            }
            return total;
        }

        /// <summary>
        ///     What Elemental Bond adds to its owner's hits with this pet out: its element's damage, or half the share as
        ///     crit chance for a pet without one. Empty without the node.
        /// </summary>
        public GearStats PetElementBonus(Element petElement)
        {
            var stats = new GearStats();
            var share = PetElementShare();
            if (share <= 0) return stats;
            if (petElement == Element.Physical) stats[GearStat.CritChance] = share / 2;
            else stats[GearStats.DamageOf(petElement)] = share;
            return stats;
        }

        /// <summary>The passives' stats, ranks counted; those that need a battle pet out (Kinship) only with one.</summary>
        public GearStats Passives(bool petOut = false)
        {
            var total = new GearStats();
            foreach (var r in Ranks)
            {
                var node = SkillTrees.Get(r.Key);
                if (node?.Passive == null || r.Value <= 0) continue;
                if (node.WhilePetOut && !petOut) continue;
                total.Add(GearStats.Parse(node.Passive), Math.Min(r.Value, node.MaxRank));
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
            if (level < node.RequiredLevel) return node.Name + " needs " + SkillTrees.SkillName(SkillTrees.SkillOf(node.Tree)) + " " + node.RequiredLevel + ".";
            // the path: one of the nodes linked below it learned (ranks already held stay, whatever the path)
            if (Rank(id) == 0 && node.Requires.Length > 0 && !node.Requires.Any(Has))
                return node.Name + " needs " + string.Join(" or ", node.Requires.Select(r => SkillTrees.Get(r)?.Name ?? r)) + " first.";
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

        /// <summary>Null when a rank can come off the node, otherwise why not (a learned node above needs it for its path).</summary>
        public string CanLower(string id)
        {
            var node = SkillTrees.Get(id);
            if (node == null || Rank(node.Id) <= 0) return "Nothing to take back.";
            if (Rank(node.Id) > 1) return null;
            foreach (var above in SkillTrees.Dependents(node.Id))
            {
                if (Has(above.Id) && !above.Requires.Any(r => !string.Equals(r, node.Id, StringComparison.OrdinalIgnoreCase) && Has(r)))
                    return above.Name + " needs " + node.Name + " for its path; take it back first.";
            }
            return null;
        }

        /// <summary>One rank off a node (the website's planner); out of the loadout when none is left.</summary>
        public void Lower(string id)
        {
            var node = SkillTrees.Get(id);
            if (node == null || Rank(node.Id) <= 0 || CanLower(id) != null) return;
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

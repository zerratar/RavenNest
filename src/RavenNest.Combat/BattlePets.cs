using System;
using System.Collections.Generic;
using System.Linq;

namespace RavenNest.Combat
{
    /// <summary>What a battle pet does in a group (the game's docs/battle-pets-design.md, section 2).</summary>
    public enum PetRole
    {
        Tank = 0,
        Healer = 1,
        Damage = 2,
        Support = 3,
        /// <summary>Helps while its owner gathers instead of fighting.</summary>
        Gatherer = 4,
    }

    /// <summary>
    ///     How a pet's attacks look, apart from its role (docs/battle-pets-design.md, section 9), so every model can fight:
    ///     a model with an attack clip bites with it, a beast without one pounces (a leap done in code), and small or
    ///     magical pets stay back and fire a bolt of their element.
    /// </summary>
    public enum PetStrike
    {
        /// <summary>Up close with the model's own attack clip.</summary>
        Bite = 0,
        /// <summary>Up close with a leap done in code: crouch, jump in, squash on the hit, hop back. Needs no clips.</summary>
        Pounce = 1,
        /// <summary>From a few metres back: a little hop and a bolt of its element.</summary>
        Bolt = 2,
    }

    /// <summary>Where a pet came from.</summary>
    public enum PetSource
    {
        /// <summary>Tamed from a creature on the islands (!tame).</summary>
        Tamed = 0,
        /// <summary>A pet item its owner has, woken to fight (!pet wake); it keeps its own look.</summary>
        Companion = 1,
        /// <summary>Hatched from an egg.</summary>
        Egg = 2,
    }

    /// <summary>What a gatherer pet's skill adds while its owner gathers.</summary>
    public enum PetGatherBonus
    {
        None = 0,
        /// <summary>Amount: the chance of one more resource per gather.</summary>
        ExtraResource,
        /// <summary>Amount: share better chance at rare drops.</summary>
        RareDrop,
        /// <summary>Amount: share faster gathering.</summary>
        Speed,
        /// <summary>Amount: the chance per gather of a bonus item from the island's list.</summary>
        BonusItem,
    }

    /// <summary>
    ///     The numbers every species of a role starts from, per pet level. A species takes its role's numbers and may
    ///     change them (a tougher tank, a faster damage pet). Levels are shares of the pet's level, the same way a
    ///     character's skill levels feed its sheet; power, accuracy and armour stand in for the gear a pet does not wear.
    /// </summary>
    [Serializable]
    public sealed class PetStatProfile
    {
        public double AimLevel = 1;
        public double DamageLevel = 1;
        public double DefenseLevel = 1;
        public double HealthLevel = 1;
        public double HealingLevel;

        /// <summary>Power per pet level, like a weapon's; a share of what a player's kit has at the same level.</summary>
        public double PowerPerLevel;
        public double AccuracyPerLevel;
        public double ArmorPerLevel;
        public double HealingPowerPerLevel;

        public double ThreatMultiplier = 1;

        /// <summary>Seconds between attacks.</summary>
        public double AttackInterval = 1.8;

        public AttackStyle Style = AttackStyle.Melee;

        public PetStatProfile Copy() => (PetStatProfile)MemberwiseClone();
    }

    /// <summary>
    ///     A pet skill (docs/battle-pets-design.md, section 5): learned at a pet level, used by itself on its cooldown
    ///     with a use rule, like a character's actives. Gatherer skills have a gather bonus instead of an active.
    /// </summary>
    public sealed class PetSkill
    {
        public string Id;
        public string Name;
        public string Description;
        public PetRole Role;

        /// <summary>The pet level that teaches it.</summary>
        public int LearnLevel = 1;

        /// <summary>Only a pet at this evolution stage or later learns it (0: every stage).</summary>
        public int Stage;

        public ActiveSkill Active;

        public PetGatherBonus Gather;
        public double GatherAmount;

        public bool IsActive => Active != null;
    }

    /// <summary>
    ///     One kind of battle pet: its role, element, stats and skills, and the three names of its evolution chain.
    ///     The species list waits for the tiered models (docs/battle-pets-design.md); until then the simulator and the
    ///     website use <see cref="BattlePets.Archetypes"/>, one stand-in per role.
    /// </summary>
    [Serializable]
    public sealed class PetSpecies
    {
        /// <summary>A stable id, such as "ember-fox".</summary>
        public string Id = "";

        /// <summary>The names of the three stages, such as { "Pebble", "Boulderback", "Mountainhide" }.</summary>
        public string[] StageNames = { "", "", "" };

        public PetRole Role;
        public Element Element = Element.Physical;

        /// <summary>The share of each hit that is <see cref="Element"/> (0 to 1).</summary>
        public double ElementShare = 0.5;

        public EnemyFamily Family = EnemyFamily.Beast;

        /// <summary>Resistance per element; a pet resists its own element a little.</summary>
        public ElementValues Resistance;

        public PetStatProfile Stats = new PetStatProfile();

        /// <summary>Its skills by id, in the order it learns them.</summary>
        public string[] Skills = Array.Empty<string>();

        /// <summary>Where its eggs come from, for people reading the list.</summary>
        public string Origin = "";

        public PetSource Source;

        /// <summary>How its attacks look (Bite needs an attack clip on the model).</summary>
        public PetStrike Strike = PetStrike.Pounce;

        /// <summary>
        ///     The game's model per stage: "enemy:Red Metalon" (an enemy's model, scaled to pet size) or "pet:Pets/Raven"
        ///     (a pet item's prefab). One entry means the same model at every stage, growing.
        /// </summary>
        public string[] StageModels = Array.Empty<string>();

        /// <summary>The pet's height in metres per stage; the game scales its model to it.</summary>
        public double[] StageHeights = { 0.6, 0.75, 0.9 };

        /// <summary>The creatures that can be tamed into it: enemy definition ids or kind names ("home-bunny", "Red Metalon").</summary>
        public string[] Creatures = Array.Empty<string>();

        /// <summary>The Taming level a try needs.</summary>
        public int TameLevel = 1;

        /// <summary>A multiplier on the tame chance: 1 for most, small for the rare ones.</summary>
        public double TameRarity = 1;

        /// <summary>Companions: the pet item's prefab ("Pets/Raven") that wakes into this species.</summary>
        public string[] ItemPrefabs = Array.Empty<string>();

        /// <summary>One line about it, for the website and !pet.</summary>
        public string Blurb = "";

        public string ModelAt(int stage) =>
            StageModels == null || StageModels.Length == 0 ? "" : StageModels[Math.Max(0, Math.Min(StageModels.Length - 1, stage))];

        /// <summary>Its height at a stage, or 0 for a companion, which keeps its item's size (grown by BattlePets.CompanionGrowth).</summary>
        public double HeightAt(int stage) =>
            StageHeights == null || StageHeights.Length == 0 ? 0 : StageHeights[Math.Max(0, Math.Min(StageHeights.Length - 1, stage))];

        /// <summary>How far from its target it attacks from.</summary>
        public double Reach => Strike == PetStrike.Bolt ? 6.0 : 1.4 + Math.Max(0.6, HeightAt(0)) * 0.5;

        public string NameAt(int stage) =>
            StageNames != null && StageNames.Length > 0 ? StageNames[Math.Max(0, Math.Min(StageNames.Length - 1, stage))] : Id;

        /// <summary>The skills a pet of this species knows at a level and stage, in learning order.</summary>
        public IEnumerable<PetSkill> SkillsAt(int level, int stage) =>
            (Skills ?? Array.Empty<string>()).Select(BattlePets.GetSkill).Where(s => s != null && s.LearnLevel <= level && s.Stage <= stage);

        /// <summary>
        ///     The sheet a pet of this species fights with: the role's numbers at its level, raised by its evolution
        ///     stage, plus the owner's pet skill bonuses applied by the caller.
        /// </summary>
        public CombatSheet Sheet(int level, int stage)
        {
            level = Math.Max(1, level);
            var p = Stats ?? new PetStatProfile();
            var m = BattlePets.StageMultiplier(stage);
            int L(double share) => Math.Max(1, (int)Math.Round(level * share * m));
            int P(double perLevel) => Math.Max(0, (int)Math.Round(level * perLevel * m));

            var sheet = new CombatSheet
            {
                CombatLevel = level,
                MaxHealth = CombatMath.MaxHealth(L(p.HealthLevel)),
                DefenseLevel = L(p.DefenseLevel),
                Armor = P(p.ArmorPerLevel),
                Resistance = Resistance,
                ThreatMultiplier = p.ThreatMultiplier,
                AttackSpeed = 1,
                // pets cap their resistances like players: no immune pets
                IsPlayer = true,
                Family = Family,
            };

            var style = sheet.Style(p.Style);
            style.DamageLevel = L(p.DamageLevel);
            style.AimLevel = L(p.AimLevel);
            style.Power = P(p.PowerPerLevel);
            style.Accuracy = P(p.AccuracyPerLevel);
            if (Element != Element.Physical && ElementShare > 0)
                style.Conversion = ElementValues.Of(Element, Math.Min(1, ElementShare));

            if (p.HealingLevel > 0)
            {
                sheet.Healing.DamageLevel = L(p.HealingLevel);
                sheet.Healing.AimLevel = L(p.HealingLevel);
                sheet.Healing.Power = P(p.HealingPowerPerLevel);
            }
            return sheet;
        }

        public PetSpecies Copy()
        {
            var copy = (PetSpecies)MemberwiseClone();
            copy.StageNames = (string[])(StageNames ?? Array.Empty<string>()).Clone();
            copy.Skills = (string[])(Skills ?? Array.Empty<string>()).Clone();
            copy.Stats = (Stats ?? new PetStatProfile()).Copy();
            copy.StageModels = (string[])(StageModels ?? Array.Empty<string>()).Clone();
            copy.StageHeights = (double[])(StageHeights ?? Array.Empty<double>()).Clone();
            copy.Creatures = (string[])(Creatures ?? Array.Empty<string>()).Clone();
            copy.ItemPrefabs = (string[])(ItemPrefabs ?? Array.Empty<string>()).Clone();
            return copy;
        }
    }

    /// <summary>
    ///     The rules of the pet skill and battle pets (docs/battle-pets-design.md). Prepared ahead of the models: the
    ///     numbers are a starting point for the simulator's "pets" scenario, and nothing in the game or on the server
    ///     uses them yet. The skill's name is not decided; the code calls it the pet skill.
    /// </summary>
    public static partial class BattlePets
    {
        /// <summary>The pet skill gains this share of the experience of the skill being trained while a pet is out; so does the pet.</summary>
        public const double ExperienceShare = 0.25;

        /// <summary>A pet at 0 health lies down and gets back up after this many seconds, with no penalty.</summary>
        public const double KnockoutSeconds = 30;

        /// <summary>The Taming levels that open a stable place each: three to begin with (a tame and a woken pet item or two), 8 in all, one out at a time.</summary>
        public static readonly int[] StableLevels = { 1, 1, 1, 30, 75, 150, 300, 500 };

        /// <summary>The longest pet name.</summary>
        public const int MaxNameLength = 24;

        /// <summary>Pet levels for the two evolutions, and the pet skill level each needs as well.</summary>
        public static readonly int[] EvolveLevels = { 30, 100 };
        public static readonly int[] EvolveSkillLevels = { 50, 150 };

        /// <summary>Base stats at each stage: +15% at the first evolution, +30% at the second.</summary>
        public static readonly double[] StageMultipliers = { 1.0, 1.15, 1.30 };

        public const int MaxStage = 2;

        public static double StageMultiplier(int stage) => StageMultipliers[Math.Max(0, Math.Min(MaxStage, stage))];

        /// <summary>A pet can never be a higher level than its owner's pet skill.</summary>
        public static int LevelCap(int petSkillLevel) => Math.Max(1, petSkillLevel);

        public static int StableSize(int petSkillLevel) => StableLevels.Count(l => petSkillLevel >= l);

        /// <summary>Skill slots: 2, 3 at pet skill level 100, 4 at 400, and one more per evolution.</summary>
        public static int SkillSlots(int petSkillLevel, int stage) =>
            (petSkillLevel >= 400 ? 4 : petSkillLevel >= 100 ? 3 : 2) + Math.Max(0, Math.Min(MaxStage, stage));

        /// <summary>Whether a pet can evolve now, and what is missing when it cannot.</summary>
        public static bool CanEvolve(int stage, int petLevel, int petSkillLevel, out string missing)
        {
            missing = null;
            if (stage >= MaxStage) { missing = "fully evolved"; return false; }
            var needLevel = EvolveLevels[stage];
            var needSkill = EvolveSkillLevels[stage];
            var parts = new List<string>();
            if (petLevel < needLevel) parts.Add("pet level " + needLevel);
            if (petSkillLevel < needSkill) parts.Add("pet skill level " + needSkill);
            if (parts.Count == 0) return true;
            missing = string.Join(" and ", parts);
            return false;
        }

        // ---------------------------------------------------------------- the role stand-ins

        /// <summary>
        ///     The numbers per role. Tuned with the simulator's "pets" scenario against the standard kit: a damage pet adds
        ///     about a third to its owner's damage, a tank pet takes most of the hits, a healer gives back a good share of
        ///     what its owner takes, a support pet adds a little of everything.
        /// </summary>
        public static PetStatProfile RoleStats(PetRole role)
        {
            switch (role)
            {
                case PetRole.Tank:
                    return new PetStatProfile { AimLevel = 0.8, DamageLevel = 0.6, DefenseLevel = 1.1, HealthLevel = 1.4, PowerPerLevel = 0.10, AccuracyPerLevel = 0.15, ArmorPerLevel = 1.4, ThreatMultiplier = 2.5, AttackInterval = 2.0 };
                case PetRole.Healer:
                    return new PetStatProfile { AimLevel = 0.7, DamageLevel = 0.5, DefenseLevel = 0.8, HealthLevel = 0.9, HealingLevel = 0.6, PowerPerLevel = 0.05, AccuracyPerLevel = 0.10, ArmorPerLevel = 0.5, HealingPowerPerLevel = 0.05, ThreatMultiplier = 0.8, AttackInterval = 2.2, Style = AttackStyle.Magic };
                case PetRole.Damage:
                    return new PetStatProfile { AimLevel = 1.0, DamageLevel = 0.7, DefenseLevel = 0.7, HealthLevel = 0.8, PowerPerLevel = 0.08, AccuracyPerLevel = 0.30, ArmorPerLevel = 0.5, ThreatMultiplier = 0.9, AttackInterval = 1.8 };
                case PetRole.Support:
                    return new PetStatProfile { AimLevel = 0.85, DamageLevel = 0.5, DefenseLevel = 0.85, HealthLevel = 1.0, PowerPerLevel = 0.05, AccuracyPerLevel = 0.20, ArmorPerLevel = 0.8, ThreatMultiplier = 1.0, AttackInterval = 2.0 };
                default:
                    return new PetStatProfile { AimLevel = 0.3, DamageLevel = 0.3, DefenseLevel = 0.6, HealthLevel = 0.8, ThreatMultiplier = 0.5, AttackInterval = 3.0 };
            }
        }

        /// <summary>One stand-in species per role until the real species exist (they wait for the models).</summary>
        public static readonly IReadOnlyList<PetSpecies> Archetypes = new[]
        {
            new PetSpecies { Id = "archetype-tank", StageNames = new[] { "Tank pet", "Tank pet II", "Tank pet III" }, Role = PetRole.Tank, Stats = RoleStats(PetRole.Tank), Skills = new[] { "guard", "harden", "bodyslam" } },
            new PetSpecies { Id = "archetype-healer", StageNames = new[] { "Healer pet", "Healer pet II", "Healer pet III" }, Role = PetRole.Healer, Element = Element.Ice, ElementShare = 0.5, Stats = RoleStats(PetRole.Healer), Skills = new[] { "lick-wounds", "soothing-aura", "purify" } },
            new PetSpecies { Id = "archetype-damage", StageNames = new[] { "Damage pet", "Damage pet II", "Damage pet III" }, Role = PetRole.Damage, Element = Element.Fire, ElementShare = 0.5, Stats = RoleStats(PetRole.Damage), Skills = new[] { "pounce", "frenzy", "fire-breath" } },
            new PetSpecies { Id = "archetype-support", StageNames = new[] { "Support pet", "Support pet II", "Support pet III" }, Role = PetRole.Support, Element = Element.Lightning, ElementShare = 0.4, Stats = RoleStats(PetRole.Support), Skills = new[] { "howl", "hex", "lick-wounds" } },
            new PetSpecies { Id = "archetype-gatherer", StageNames = new[] { "Gatherer pet", "Gatherer pet II", "Gatherer pet III" }, Role = PetRole.Gatherer, Stats = RoleStats(PetRole.Gatherer), Skills = new[] { "forage", "keen-nose", "dig" } },
        };

        // ---------------------------------------------------------------- the skills

        private static readonly Dictionary<string, PetSkill> skillsById = new Dictionary<string, PetSkill>(StringComparer.OrdinalIgnoreCase);
        private static readonly List<PetSkill> skills = new List<PetSkill>();

        public static IReadOnlyList<PetSkill> Skills => skills;

        public static PetSkill GetSkill(string id) => id != null && skillsById.TryGetValue(id.Trim(), out var s) ? s : null;

        /// <summary>A skill by id or name ("lick-wounds", "Lick Wounds"), for chat commands.</summary>
        public static PetSkill FindSkill(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            var t = text.Trim();
            return GetSkill(t) ?? GetSkill(t.Replace(' ', '-')) ?? skills.FirstOrDefault(s => string.Equals(s.Name, t, StringComparison.OrdinalIgnoreCase));
        }

        private static void Add(PetSkill skill)
        {
            skills.Add(skill);
            skillsById[skill.Id] = skill;
        }

        private static ActiveSkill Active(double cooldown, SkillTargeting targeting, SkillUse use, params SkillEffect[] effects) =>
            new ActiveSkill { Cooldown = cooldown, Targeting = targeting, Use = use, Effects = effects };

        private static ActiveSkill Area(double cooldown, SkillTargeting targeting, SkillUse use, int maxTargets, double radius, params SkillEffect[] effects) =>
            new ActiveSkill { Cooldown = cooldown, Targeting = targeting, Use = use, MaxTargets = maxTargets, Radius = radius, Effects = effects };

        private static SkillEffect E(SkillEffectKind kind, double amount = 0, double seconds = 0, Element? element = null) =>
            new SkillEffect(kind, amount, seconds) { Element = element };

        static BattlePets()
        {
            // tank
            Add(new PetSkill { Id = "guard", Name = "Guard", Role = PetRole.Tank, LearnLevel = 1,
                Description = "Taunts the enemies attacking its owner for 5 s.",
                Active = Active(14, SkillTargeting.Around, SkillUse.EnemyOnAlly, E(SkillEffectKind.Taunt, 0, 5)) });
            Add(new PetSkill { Id = "harden", Name = "Harden", Role = PetRole.Tank, LearnLevel = 20,
                Description = "Takes 40% less damage for 5 s.",
                Active = Active(20, SkillTargeting.Self, SkillUse.SelfHurt, E(SkillEffectKind.DamageTaken, -0.4, 5)) });
            Add(new PetSkill { Id = "bodyslam", Name = "Bodyslam", Role = PetRole.Tank, LearnLevel = 60,
                Description = "A heavy hit that stuns the target for 2 s.",
                Active = Active(18, SkillTargeting.Target, SkillUse.InFight, E(SkillEffectKind.Damage, 1.2), E(SkillEffectKind.Stun, 0, 2)) });

            // healer
            Add(new PetSkill { Id = "lick-wounds", Name = "Lick Wounds", Role = PetRole.Healer, LearnLevel = 1,
                Description = "Heals the most hurt of its owner and allies near.",
                Active = Active(8, SkillTargeting.LowestAlly, SkillUse.AllyHurt, E(SkillEffectKind.Heal, 1.5)) });
            Add(new PetSkill { Id = "soothing-aura", Name = "Soothing Aura", Role = PetRole.Healer, LearnLevel = 25,
                Description = "Heals everyone near over 8 s.",
                Active = Area(24, SkillTargeting.Allies, SkillUse.AllyHurt, 5, 6, E(SkillEffectKind.HealOverTime, 2.5, 8)) });
            Add(new PetSkill { Id = "purify", Name = "Purify", Role = PetRole.Healer, LearnLevel = 60,
                Description = "Takes burn, chill, poison and bleed off an ally.",
                Active = Active(16, SkillTargeting.LowestAlly, SkillUse.AllyHarmed, E(SkillEffectKind.Cleanse)) });

            // damage
            Add(new PetSkill { Id = "pounce", Name = "Pounce", Role = PetRole.Damage, LearnLevel = 1,
                Description = "Leaps at the target for a big hit.",
                Active = Active(12, SkillTargeting.Target, SkillUse.InFight, E(SkillEffectKind.Damage, 2.0)) });
            Add(new PetSkill { Id = "frenzy", Name = "Frenzy", Role = PetRole.Damage, LearnLevel = 25,
                Description = "Attacks 40% faster for 6 s.",
                Active = Active(20, SkillTargeting.Self, SkillUse.InFight, E(SkillEffectKind.AttackSpeed, 0.4, 6)) });
            Add(new PetSkill { Id = "fire-breath", Name = "Fire Breath", Role = PetRole.Damage, LearnLevel = 60,
                Description = "Breathes fire on the enemies in front of it, and they burn.",
                Active = Area(16, SkillTargeting.Cone, SkillUse.InFight, 4, 4, E(SkillEffectKind.Damage, 1.3, 0, Element.Fire), E(SkillEffectKind.DamageOverTime, 0.8, 4, Element.Fire)) });

            // support
            Add(new PetSkill { Id = "howl", Name = "Howl", Role = PetRole.Support, LearnLevel = 1,
                Description = "Allies near attack 15% faster for 8 s.",
                Active = Area(22, SkillTargeting.Allies, SkillUse.InFight, 5, 8, E(SkillEffectKind.AttackSpeed, 0.15, 8)) });
            Add(new PetSkill { Id = "hex", Name = "Hex", Role = PetRole.Support, LearnLevel = 25,
                Description = "The target takes 12% more damage for 8 s.",
                Active = Active(18, SkillTargeting.Target, SkillUse.InFight, E(SkillEffectKind.DamageTaken, 0.12, 8)) });

            // gatherer: helps while its owner gathers instead
            Add(new PetSkill { Id = "forage", Name = "Forage", Role = PetRole.Gatherer, LearnLevel = 1,
                Description = "A 5% chance of one more resource per gather.", Gather = PetGatherBonus.ExtraResource, GatherAmount = 0.05 });
            Add(new PetSkill { Id = "keen-nose", Name = "Keen Nose", Role = PetRole.Gatherer, LearnLevel = 25,
                Description = "10% better chance at rare drops while gathering.", Gather = PetGatherBonus.RareDrop, GatherAmount = 0.10 });
            Add(new PetSkill { Id = "dig", Name = "Dig", Role = PetRole.Gatherer, LearnLevel = 60,
                Description = "Now and then digs up a bonus item from the island.", Gather = PetGatherBonus.BonusItem, GatherAmount = 0.01 });

            // the species' own skills (PetSpeciesList.cs)
            Add(new PetSkill { Id = "moo", Name = "Moo", Role = PetRole.Tank, LearnLevel = 1,
                Description = "Moos at the enemies attacking its owner. They cannot help but look, for 5 s.",
                Active = Area(14, SkillTargeting.Around, SkillUse.EnemyOnAlly, 6, 6, E(SkillEffectKind.Taunt, 0, 5)) });
            Add(new PetSkill { Id = "binky", Name = "Binky", Role = PetRole.Support, LearnLevel = 1,
                Description = "A happy little leap in the air. Its owner and allies near attack 20% faster for 6 s.",
                Active = Area(20, SkillTargeting.Allies, SkillUse.InFight, 3, 5, E(SkillEffectKind.AttackSpeed, 0.2, 6)) });
            Add(new PetSkill { Id = "venom-sting", Name = "Venom Sting", Role = PetRole.Damage, LearnLevel = 1,
                Description = "Stings the target with its tail; the poison works for 6 s.",
                Active = Active(14, SkillTargeting.Target, SkillUse.InFight, E(SkillEffectKind.Damage, 0.8, 0, Element.Poison), E(SkillEffectKind.DamageOverTime, 1.4, 6, Element.Poison)) });
            Add(new PetSkill { Id = "frost-breath", Name = "Frost Breath", Role = PetRole.Damage, LearnLevel = 60,
                Description = "Breathes frost on the enemies in front of it, and they slow.",
                Active = Area(16, SkillTargeting.Cone, SkillUse.InFight, 4, 4, E(SkillEffectKind.Damage, 1.6, 0, Element.Ice)) });
            Add(new PetSkill { Id = "venom-breath", Name = "Venom Breath", Role = PetRole.Damage, LearnLevel = 60,
                Description = "Breathes a poison cloud on the enemies in front of it.",
                Active = Area(16, SkillTargeting.Cone, SkillUse.InFight, 4, 4, E(SkillEffectKind.Damage, 0.8, 0, Element.Poison), E(SkillEffectKind.DamageOverTime, 1.4, 6, Element.Poison)) });
            Add(new PetSkill { Id = "storm-breath", Name = "Storm Breath", Role = PetRole.Damage, LearnLevel = 60,
                Description = "Breathes lightning that jumps between enemies.",
                Active = Area(16, SkillTargeting.Chain, SkillUse.InFight, 4, 6, E(SkillEffectKind.Damage, 1.4, 0, Element.Lightning)) });
            Add(new PetSkill { Id = "wail", Name = "Wail", Role = PetRole.Support, LearnLevel = 25,
                Description = "A cold wail: enemies near deal 15% less damage for 6 s.",
                Active = Area(20, SkillTargeting.Around, SkillUse.InFight, 5, 6, E(SkillEffectKind.DamageDealt, -0.15, 6)) });
            Add(new PetSkill { Id = "quack", Name = "QUACK", Role = PetRole.Damage, LearnLevel = 1,
                Description = "QUACK. The enemies near are stunned for 1.5 s, from sheer confusion.",
                Active = Area(25, SkillTargeting.Around, SkillUse.EnemiesNear, 5, 5, E(SkillEffectKind.Stun, 0, 1.5)) });
            Add(new PetSkill { Id = "banana-toss", Name = "Banana Toss", Role = PetRole.Damage, LearnLevel = 1,
                Description = "Throws a banana peel at the target's feet. It hurts, and they slip for 1 s.",
                Active = Active(14, SkillTargeting.Target, SkillUse.InFight, E(SkillEffectKind.Damage, 1.3), E(SkillEffectKind.Stun, 0, 1)) });
            Add(new PetSkill { Id = "barrage", Name = "Barrage", Role = PetRole.Damage, LearnLevel = 1,
                Description = "Three quick bolts at the target.",
                Active = Active(12, SkillTargeting.Target, SkillUse.InFight, new SkillEffect(SkillEffectKind.Damage, 0.7) { Hits = 3 }) });
            Add(new PetSkill { Id = "spark", Name = "Spark", Role = PetRole.Support, LearnLevel = 1,
                Description = "A spark at the target: it takes 10% more damage for 6 s.",
                Active = Active(16, SkillTargeting.Target, SkillUse.InFight, E(SkillEffectKind.Damage, 0.8), E(SkillEffectKind.DamageTaken, 0.10, 6)) });
        }
    }
}

using System;
namespace RavenNest.Combat
{
    public enum AttackStyle
    {
        Melee = 0,
        Ranged = 1,
        Magic = 2,
        Healing = 3,
    }

    /// <summary>What one attack style has to work with: the skills behind it and the gear's numbers for it.</summary>
    public sealed class StyleStats
    {
        /// <summary>The skill that sets how hard it hits: Strength for melee, Ranged, Magic, Healing.</summary>
        public int DamageLevel;

        /// <summary>The skill that sets how often it hits: Attack for melee, Ranged, Magic.</summary>
        public int AimLevel;

        /// <summary>The gear's power for this style (today's WeaponPower, RangedPower, MagicPower).</summary>
        public int Power;

        /// <summary>The gear's accuracy for this style (today's WeaponAim, RangedAim, MagicAim).</summary>
        public int Accuracy;

        /// <summary>
        ///     The share of a hit that turns into each element instead of physical (a fire staff converts some
        ///     of its hit to fire). Shares above 1 in total are treated as 1.
        /// </summary>
        public ElementValues Conversion;

        /// <summary>Shares more power and accuracy (passives, gear): power times 1 + PowerBonus.</summary>
        public double PowerBonus;
        public double AccuracyBonus;

        public int EffectivePower => (int)(Power * (1 + PowerBonus));
        public int EffectiveAccuracy => (int)(Accuracy * (1 + AccuracyBonus));

        public StyleStats Copy() => (StyleStats)MemberwiseClone();
    }

    /// <summary>
    ///     Everything a fighter brings to a blow, built from skills, gear, gems, enchantments, skill tree passives
    ///     and effects (docs/combat-v2-design.md, section 1). Players, pets and enemies all use one.
    /// </summary>
    public sealed class CombatSheet
    {
        /// <summary>The fighter's combat level.</summary>
        public int CombatLevel = 1;

        public int MaxHealth = 100;

        public StyleStats Melee = new StyleStats();
        public StyleStats Ranged = new StyleStats();
        public StyleStats Magic = new StyleStats();
        public StyleStats Healing = new StyleStats();

        /// <summary>The Defense level, which sets how often blows miss you.</summary>
        public int DefenseLevel;

        /// <summary>Evasion from gear and passives, on top of what Defense gives.</summary>
        public int GearEvasion;

        /// <summary>Takes a share off physical damage.</summary>
        public int Armor;

        /// <summary>Extra damage per element, as a share of each hit (a ruby in a sword adds some fire to every blow).</summary>
        public ElementValues ElementBonus;

        /// <summary>
        ///     Resistance per element as a share: 0.3 takes 30% off, a negative value is a weakness that takes
        ///     extra, 1 is immune. Physical resistance is rare (stone golems); armour is the usual answer to it.
        /// </summary>
        public ElementValues Resistance;

        public double CritChance = CombatMath.BaseCritChance;
        public double CritDamage = CombatMath.BaseCritDamage;

        // multipliers from buffs, debuffs and passives; 1 means none
        public double DamageMultiplier = 1;
        public double DamageTakenMultiplier = 1;
        public double HealingMultiplier = 1;
        public double ThreatMultiplier = 1;

        /// <summary>Attacks per second times this (1.1 swings 10% more often); the game divides swing times by it.</summary>
        public double AttackSpeed = 1;

        /// <summary>The share of a melee blow taken that hurts the attacker back.</summary>
        public double Thorns;

        /// <summary>The share of damage dealt that heals the attacker.</summary>
        public double LifeSteal;

        /// <summary>The share of the target's armour the fighter's blows ignore.</summary>
        public double ArmorPenetration;

        // the skill trees' passives (and gear that carries the same stats); 0 means none
        public double ArmorBonus;
        public double BlockChance;
        public double CastSpeed = 1;
        public double HealingReceived;
        public double BossDamage;
        public double ExecuteDamage;
        public double LowHealthReduction;
        public double BleedOnCrit;
        public double HealShield;
        public double CombatRegen;
        public double BurnBonus;
        public double ResistanceShred;
        public double LootLuck;
        public double CrowdControlDuration;
        public double UndeadDamage;
        /// <summary>A share off the crit chance of blows against this fighter (gems).</summary>
        public double CritResistance;
        public double PoisonBonus;
        public double LightningJumpBonus;
        /// <summary>A share off combat skill cooldowns (capped at <see cref="CombatMath.MaxCooldownReduction"/>).</summary>
        public double CooldownReduction;

        /// <summary>Health left as a share of the pool (1 is full): for execute damage and low health passives.</summary>
        public double CurrentHealthShare = 1;

        /// <summary>An enemy's family (undead take more from UndeadDamage); null for players.</summary>
        public EnemyFamily? Family;

        public int EffectiveArmor => (int)(Armor * (1 + ArmorBonus));

        /// <summary>Players cap their resistances (75%); enemies may be immune.</summary>
        public bool IsPlayer;

        /// <summary>Bosses shrug off crowd control sooner (shorter, then immune for a while).</summary>
        public bool IsBoss;

        public StyleStats Style(AttackStyle style)
        {
            switch (style)
            {
                case AttackStyle.Ranged: return Ranged;
                case AttackStyle.Magic: return Magic;
                case AttackStyle.Healing: return Healing;
                default: return Melee;
            }
        }

        /// <summary>
        ///     Level sync (the tower, docs/tower-design.md section 12): no skill level counts above <paramref name="cap"/>;
        ///     gear, skill trees and gems stay as they are. The health pool shrinks to the cap's, keeping what gear adds.
        /// </summary>
        public void CapLevels(int cap, int gearHealth = 0)
        {
            if (cap <= 0) return;
            CombatLevel = Math.Min(CombatLevel, cap);
            DefenseLevel = Math.Min(DefenseLevel, cap);
            foreach (var s in new[] { Melee, Ranged, Magic, Healing })
            {
                s.AimLevel = Math.Min(s.AimLevel, cap);
                s.DamageLevel = Math.Min(s.DamageLevel, cap);
            }
            var pool = IsPlayer ? CombatMath.PlayerMaxHealth(cap) : CombatMath.MaxHealth(cap);
            MaxHealth = Math.Min(MaxHealth, pool + Math.Max(0, gearHealth));
        }

        public CombatSheet Copy()
        {
            var copy = (CombatSheet)MemberwiseClone();
            copy.Melee = Melee.Copy();
            copy.Ranged = Ranged.Copy();
            copy.Magic = Magic.Copy();
            copy.Healing = Healing.Copy();
            return copy;
        }
    }
}

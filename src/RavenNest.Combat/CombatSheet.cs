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

        public StyleStats Copy() => (StyleStats)MemberwiseClone();
    }

    /// <summary>
    ///     Everything a fighter brings to a blow, built from skills, gear, gems, enchantments, skill tree passives
    ///     and effects (docs/combat-v2-design.md, section 1). Players, pets and enemies all use one.
    /// </summary>
    public sealed class CombatSheet
    {
        /// <summary>The fighter's combat level, which armour is measured against.</summary>
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

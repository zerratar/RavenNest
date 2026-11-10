using System;

namespace RavenNest.Combat
{
    /// <summary>What kind of creature an enemy is; later used by skills, Slayer and loot.</summary>
    public enum EnemyFamily
    {
        Beast = 0,
        Humanoid = 1,
        Undead = 2,
        Spirit = 3,
        Dragon = 4,
        Insect = 5,
        Construct = 6,
    }

    /// <summary>
    ///     One kind of enemy (the game's docs/combat-v2-design.md, section 6): its levels, what it fights with, the
    ///     element of its attacks, what it resists and what it is weak to, and the skills it uses. The game keeps one
    ///     asset per kind and applies it to every enemy of that kind; the simulator reads the same definitions from
    ///     the game's export. Public fields only, so Unity and System.Text.Json both store it as it is.
    /// </summary>
    [Serializable]
    public sealed class EnemyDefinition
    {
        /// <summary>A stable id, such as "atria-gladiator".</summary>
        public string Id = "";

        /// <summary>The kind's name, such as "Gladiator". Named enemies keep their own names in the game.</summary>
        public string Name = "";

        /// <summary>Where it lives: the island and fighting area, for people reading the list.</summary>
        public string Area = "";

        public EnemyFamily Family;

        // skill levels, as on a character
        public int Attack = 1;
        public int Strength = 1;
        public int Defense = 1;
        public int Health = 10;
        public int Ranged = 1;
        public int Magic = 1;
        public int Healing = 1;

        /// <summary>Power of its attacks, like a weapon's. Today's enemies have none.</summary>
        public int Power;

        /// <summary>Accuracy of its attacks, like a weapon's. Today's enemies have none.</summary>
        public int Accuracy;

        /// <summary>Armour, which takes a share off physical damage. Today's enemies have none.</summary>
        public int Armor;

        /// <summary>How it attacks. Ranged and magic enemies keep their distance.</summary>
        public AttackStyle Style = AttackStyle.Melee;

        /// <summary>The element of its attacks.</summary>
        public Element Element = Element.Physical;

        /// <summary>The share of each hit that is <see cref="Element"/> instead of physical (0 to 1).</summary>
        public double ElementShare;

        /// <summary>Resistance per element: 0.5 takes half off, a negative value is a weakness, 1 is immune.</summary>
        public ElementValues Resistance;

        /// <summary>The skills it uses on its own (ids from the skill list; phase 2 of the design).</summary>
        public string[] Skills = Array.Empty<string>();

        /// <summary>Seconds between attacks.</summary>
        public double AttackInterval = 1;

        /// <summary>Bosses shrug off crowd control sooner.</summary>
        public bool IsBoss;

        /// <summary>
        ///     Its levels are set by the encounter (dungeon and raid scaling), not by this definition; style, element,
        ///     resistances and skills still come from here.
        /// </summary>
        public bool Scaled;

        /// <summary>The combat level the game shows, from the same formula as a character's.</summary>
        public int CombatLevel => (int)((Attack + Defense + Strength + Health) / 4f + (Ranged + Magic + Healing) / 8f);

        /// <summary>The sheet the combat rules use for this enemy.</summary>
        public CombatSheet Sheet()
        {
            var sheet = new CombatSheet
            {
                CombatLevel = CombatLevel,
                MaxHealth = CombatMath.MaxHealth(Health),
                DefenseLevel = Defense,
                Armor = Armor,
                Resistance = Resistance,
                IsPlayer = false,
                IsBoss = IsBoss,
                Family = Family,
            };

            sheet.Melee.DamageLevel = Strength;
            sheet.Melee.AimLevel = Attack;
            sheet.Ranged.DamageLevel = Ranged;
            sheet.Ranged.AimLevel = Ranged;
            sheet.Magic.DamageLevel = Magic;
            sheet.Magic.AimLevel = Magic;
            sheet.Healing.DamageLevel = Healing;
            sheet.Healing.AimLevel = Healing;

            var style = sheet.Style(Style);
            style.Power = Power;
            style.Accuracy = Accuracy;
            ApplyElement(sheet);
            return sheet;
        }

        /// <summary>Puts the definition's attack element and resistances on a sheet built elsewhere (the game's).</summary>
        public void ApplyElement(CombatSheet sheet)
        {
            sheet.Resistance = Resistance;
            sheet.IsBoss = IsBoss;
            sheet.Family = Family;
            if (Element != Element.Physical && ElementShare > 0)
            {
                var share = ElementShare > 1 ? 1 : ElementShare;
                sheet.Style(Style).Conversion = ElementValues.Of(Element, share);
            }
        }

        public EnemyDefinition Copy()
        {
            var copy = (EnemyDefinition)MemberwiseClone();
            copy.Skills = (string[])(Skills ?? Array.Empty<string>()).Clone();
            return copy;
        }

        /// <summary>
        ///     The element a creature of <paramref name="element"/> is weak to, the way the island enemies are set up:
        ///     fire and ice against each other, lightning and poison against each other.
        /// </summary>
        public static Element Weakness(Element element)
        {
            switch (element)
            {
                case Element.Fire: return Element.Ice;
                case Element.Ice: return Element.Fire;
                case Element.Lightning: return Element.Poison;
                case Element.Poison: return Element.Lightning;
                default: return Element.Physical;
            }
        }

        /// <summary>Raid and dungeon bosses roll this share of their hits as an element (the rest of a roll stays physical).</summary>
        public const double BossElementShare = 0.4;

        /// <summary>Of raid and dungeon bosses, this many fight with plain physical blows; the rest pick an element.</summary>
        public const double BossPhysicalChance = 0.4;

        /// <summary>
        ///     A copy fighting with <paramref name="element"/>: that share of every hit, half its resistance to it and a
        ///     quarter weakness to the opposite element (as the island enemies of that element have). Physical: a plain copy.
        /// </summary>
        public EnemyDefinition WithElement(Element element, double share)
        {
            var copy = Copy();
            copy.Element = element;
            copy.ElementShare = element == Element.Physical ? 0 : share;
            if (element != Element.Physical)
                copy.Resistance = ElementValues.Of(element, 0.5) + ElementValues.Of(Weakness(element), -0.25);
            return copy;
        }

        /// <summary>
        ///     A raid or dungeon boss's element for one fight from a roll in [0, 1): plain physical below
        ///     <see cref="BossPhysicalChance"/>, then fire, ice, lightning and poison in equal parts.
        /// </summary>
        public static Element BossElement(double roll)
        {
            if (roll < BossPhysicalChance) return Element.Physical;
            var rest = (roll - BossPhysicalChance) / (1 - BossPhysicalChance);
            var index = (int)(rest * 4);
            if (index > 3) index = 3;
            return Elements.All[1 + index];
        }
    }
}

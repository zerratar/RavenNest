using System;

namespace RavenNest.Combat
{
    /// <summary>
    ///     The kinds of damage (docs/combat-v2-design.md, section 2). Physical is reduced by armour, the others by
    ///     their resistance, and each element has an effect of its own on hit.
    /// </summary>
    public enum Element
    {
        Physical = 0,
        Fire = 1,
        Ice = 2,
        Lightning = 3,
        Poison = 4,
    }

    public static class Elements
    {
        public const int Count = 5;

        public static readonly Element[] All = { Element.Physical, Element.Fire, Element.Ice, Element.Lightning, Element.Poison };
    }

    /// <summary>
    ///     One number per element: element damage shares, resistances, or damage dealt by element. A struct, so a
    ///     sheet copies cleanly and nothing allocates in a fight.
    /// </summary>
    [Serializable]
    public struct ElementValues
    {
        public double Physical;
        public double Fire;
        public double Ice;
        public double Lightning;
        public double Poison;

        public double this[Element element]
        {
            get
            {
                switch (element)
                {
                    case Element.Fire: return Fire;
                    case Element.Ice: return Ice;
                    case Element.Lightning: return Lightning;
                    case Element.Poison: return Poison;
                    default: return Physical;
                }
            }
            set
            {
                switch (element)
                {
                    case Element.Fire: Fire = value; break;
                    case Element.Ice: Ice = value; break;
                    case Element.Lightning: Lightning = value; break;
                    case Element.Poison: Poison = value; break;
                    default: Physical = value; break;
                }
            }
        }

        public double Total => Physical + Fire + Ice + Lightning + Poison;

        public static ElementValues operator +(ElementValues a, ElementValues b) => new ElementValues
        {
            Physical = a.Physical + b.Physical,
            Fire = a.Fire + b.Fire,
            Ice = a.Ice + b.Ice,
            Lightning = a.Lightning + b.Lightning,
            Poison = a.Poison + b.Poison,
        };

        public static ElementValues operator *(ElementValues a, double scale) => new ElementValues
        {
            Physical = a.Physical * scale,
            Fire = a.Fire * scale,
            Ice = a.Ice * scale,
            Lightning = a.Lightning * scale,
            Poison = a.Poison * scale,
        };

        public static ElementValues Of(Element element, double value)
        {
            var values = new ElementValues();
            values[element] = value;
            return values;
        }

        public override string ToString() =>
            $"physical {Physical:0.##}, fire {Fire:0.##}, ice {Ice:0.##}, lightning {Lightning:0.##}, poison {Poison:0.##}";
    }

    /// <summary>The source of chance for combat, so the simulator and the tests can be seeded and the game can use its own.</summary>
    public interface ICombatRandom
    {
        /// <summary>A number from 0 (included) to 1 (excluded).</summary>
        double Next();
    }

    public sealed class SeededCombatRandom : ICombatRandom
    {
        private readonly Random random;

        public SeededCombatRandom(int seed) => random = new Random(seed);

        public double Next() => random.NextDouble();
    }
}

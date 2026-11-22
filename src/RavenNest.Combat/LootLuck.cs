using System;

namespace RavenNest.Combat
{
    /// <summary>
    ///     Loot luck (GearStat.LootLuck: Treasure Hunter, Legend of the Hunt, Fortune's Favorite): "a share more loot
    ///     in raids and dungeons". The server rolls it where a raid's or a dungeon's item is rolled: the chance of the
    ///     item times 1 + luck, and where that passes a sure thing (a dungeon's item often is one) what is over is
    ///     the chance of one more item. So 20% luck is 20% more items on average, whatever the chance was.
    ///     Until 2026-11-22 nothing rolled it: the stat was shown and did nothing.
    /// </summary>
    public static class LootLuck
    {
        /// <summary>The most luck that counts, so no stack of gear and skills doubles the loot.</summary>
        public const double Max = 0.5;

        /// <summary>The chance of the item, and of one more, for a base chance and a character's luck.</summary>
        public static void Chances(double baseChance, double luck, out double first, out double second)
        {
            var expected = Math.Max(0, Math.Min(1, baseChance)) * (1 + Math.Max(0, Math.Min(Max, luck)));
            first = Math.Min(1, expected);
            second = Math.Max(0, expected - 1);
        }

        /// <summary>How many items two rolls (0 to 1 each) give: none, one, or two.</summary>
        public static int Items(double baseChance, double luck, double firstRoll, double secondRoll)
        {
            Chances(baseChance, luck, out var first, out var second);
            if (firstRoll > first) return 0;
            return second > 0 && secondRoll < second ? 2 : 1;
        }

        /// <summary>A build's loot luck (its passives; gear does not carry the stat today).</summary>
        public static double Of(SkillBuild build) => build == null ? 0 : Math.Max(0, build.Passives()[GearStat.LootLuck]);
    }
}

namespace RavenNest.BusinessLogic.Game
{
    /// <summary>
    /// What each town hall tier needs: a village level and resources. Tiers are numbered from 0 (the
    /// longhouse) to 4 (the castle), the same as the game client's town hall tiers.
    /// </summary>
    /// <remarks>
    /// First draft numbers (2026-10-12), to be tuned from play. A gathering player adds one resource to
    /// the village every 10 seconds, 360 an hour, so a small stream with three players on a resource earns
    /// about a thousand of it an hour. The level gates are the levels that used to raise the tier on their
    /// own. Coins are left out until the village has a coin income.
    /// </remarks>
    public static class TownHallTiers
    {
        public const int MaxTier = 4;

        private static readonly Requirement[] requirements =
        {
            new Requirement(0, 0, 0, 0, 0),                    // tier 0, the longhouse: every village starts here
            new Requirement(10, 2_000, 1_000, 0, 0),            // tier 1, town hall with a bell tower
            new Requirement(30, 8_000, 6_000, 3_000, 3_000),    // tier 2, guild hall
            new Requirement(80, 20_000, 30_000, 10_000, 10_000),// tier 3, keep
            new Requirement(150, 60_000, 90_000, 30_000, 30_000)// tier 4, castle
        };

        /// <summary>What it takes to have <paramref name="tier"/>. Null outside 0 to MaxTier.</summary>
        public static Requirement Get(int tier)
        {
            return tier >= 0 && tier <= MaxTier ? requirements[tier] : null;
        }

        /// <summary>
        /// The tier the village level gave before tiers were bought. A village keeps it when buying starts,
        /// so nobody loses the hall they had.
        /// </summary>
        public static int FromLevel(int level)
        {
            if (level < 10) return 0;
            if (level < 30) return 1;
            if (level < 80) return 2;
            if (level < 150) return 3;
            return 4;
        }

        public class Requirement
        {
            public Requirement(int level, int wood, int ore, int fish, int wheat)
            {
                Level = level;
                Wood = wood;
                Ore = ore;
                Fish = fish;
                Wheat = wheat;
            }

            public int Level { get; }
            public int Wood { get; }
            public int Ore { get; }
            public int Fish { get; }
            public int Wheat { get; }
        }
    }
}

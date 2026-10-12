using System;

namespace RavenNest.BusinessLogic.Game
{
    /// <summary>
    /// Coins a village earns when its players defeat a raid or a dungeon, paid on top of the players' own
    /// rewards, never taken from them, so helping out never feels like losing something.
    /// </summary>
    /// <remarks>
    /// First draft numbers (2026-10-12), to be tuned from play. Village coins are not spent on anything yet;
    /// they are meant to join the town hall upgrade costs later, so these mainly set the scale. More players
    /// and harder dungeons pay more, and the turnout counts up to <see cref="MaxParticipants"/> so a very
    /// large stream does not run away with it.
    /// </remarks>
    public static class VillageCoinRewards
    {
        public const int MaxParticipants = 50;

        public const int RaidBase = 500;
        public const int RaidPerParticipant = 50;

        public const int DungeonBase = 800;
        public const int DungeonPerParticipant = 60;

        /// <summary>Coins for a defeated raid with <paramref name="participants"/> players rewarded.</summary>
        public static int ForRaid(int participants)
        {
            if (participants <= 0) return 0;
            return RaidBase + RaidPerParticipant * Math.Min(participants, MaxParticipants);
        }

        /// <summary>
        /// Coins for a defeated dungeon. Tier is the dungeon tier the game sends: common 0, uncommon 1, rare 2,
        /// epic 3, legendary 4 (dynamic dungeons, 5, count as legendary).
        /// </summary>
        public static int ForDungeon(int tier, int participants)
        {
            if (participants <= 0) return 0;
            var multiplier = 1.0 + 0.5 * Math.Clamp(tier, 0, 4);
            return (int)Math.Round((DungeonBase + DungeonPerParticipant * Math.Min(participants, MaxParticipants)) * multiplier);
        }
    }
}

namespace RavenNest.Combat
{
    /// <summary>
    ///     Getting back into a boss fight after being knocked out (the game's docs/tower-worlds-proposal.md, section
    ///     13; the owner, 2026-11-24): a knocked out player gets up by the part's entrance, recovers there, and walks
    ///     back into the fight by themselves after <see cref="Seconds"/>, at <see cref="ReturnHealth"/> of their
    ///     health. An attempt is lost when nobody stands at the same moment (everyone knocked out or recovering), or
    ///     when its time runs out.
    ///     One fixed time, the same on every floor and for every knockout (a time that grew with the floor would be
    ///     minutes long on floor 100): how hard a floor's guardian is belongs in the guardian's own numbers. One
    ///     rule here: the game applies it and the simulator's "tower" fights with it ("tower 200 norecover" fights
    ///     as before it, "tower 200 REC=60:0.5" with other numbers).
    /// </summary>
    public static class TowerRecovery
    {
        /// <summary>
        ///     Seconds from a knockout until the player goes back into the fight. The owner's 40: in the simulator a
        ///     fresh group of five in the best gear then wins every guardian (before there was any recovery it lost
        ///     the harder mini bosses one time in two to four), and so it does with 60; with 90 it loses the harder
        ///     ones about one time in ten. Two players win 84 to 100 of 100 with 40, 71 to 100 with 60.
        /// </summary>
        public const double Seconds = 40;

        /// <summary>The share of their health a player comes back with.</summary>
        public const double ReturnHealth = 0.5;

        /// <summary>Seconds from getting back up to the first blow on the guardian again: the walk back to it (the simulator's).</summary>
        public const double WalkBackSeconds = 5;
    }
}

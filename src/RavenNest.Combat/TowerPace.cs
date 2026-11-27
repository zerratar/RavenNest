using System;
using System.Collections.Generic;

namespace RavenNest.Combat
{
    /// <summary>
    ///     The pace of a part's objectives by the size of the group (the game's docs/tower-worlds-proposal.md, section
    ///     13). A part's amounts are fixed ("Defeat 300 Moss Goblins"), and half of a kill is walking to the next
    ///     monster, so without this two players needed twice the time of five and thirty a quarter to a half of it.
    ///     So each counted kill, and each counted gathering round, is worth what makes the time come out as aimed: a
    ///     kill by the suggested group of five counts as one, by two players about 1.1, by ten 0.8 to 1 and by thirty
    ///     0.7 to 1.2 by the floor (a crowd's monsters have more health the higher the floor, so its kills come
    ///     slower). The amounts shown stay as they are; the server keeps the parts of a kill and shows whole numbers.
    ///     The aim (<see cref="TimeShare"/>): two players need about 1.4 times the time of five, ten about 0.8 times,
    ///     thirty about 0.6 times.
    ///     One rule here, next to <see cref="TowerPressure"/>: the server applies it to what the game reports, and the
    ///     simulator's "tower" shows the minutes it gives.
    /// </summary>
    public static class TowerPace
    {
        /// <summary>
        ///     How the time for a part's objectives follows the group under the suggested group and over it: the time
        ///     is (heads / five) to the power of minus this. 0.367 makes two players 1.4 times five's; 0.26 makes ten
        ///     0.84 times and thirty 0.63 times.
        /// </summary>
        public const double SmallGroupExponent = 0.367;
        public const double BigGroupExponent = 0.26;

        /// <summary>
        ///     The share of a kill that is fighting for the suggested group (the rest is walking to the next monster,
        ///     which no scaling changes). It was 0.44 when every spot held one monster that stood still: five players
        ///     made 12 kills a minute on floor 1, part 6. Since packs of two or three stand on every spot, wander, and go
        ///     for players near them (2026-11-27), five players make about 21 a minute on floors 1 and 2 and the walk is
        ///     a small part of a kill: bots on floor 2, part 2 made 13.6, 19.6, 26.5 and 35.8 kills a minute with 2, 4,
        ///     9.4 and 26.9 heads, which this share fits within 4% (0.8 and 0.9 within 5%, 0.6 within 15%).
        /// </summary>
        public const double FightShare = 0.85;

        /// <summary>A lone player counts as one head at least, however far below the part (nothing to gain by being weak).</summary>
        public const double MinHeads = 1;

        /// <summary>The gatherers a stream of the suggested group is taken to have: their rounds count as they are.</summary>
        public const double SuggestedGatherers = 2;

        /// <summary>
        ///     The heads of a group from each player's strength (<see cref="TowerPressure.Strength"/>): a player counts
        ///     as one, one far below the part as that share of a head, a quarter at least
        ///     (<see cref="TowerPressure.MinHead"/>), as for pressure. What a group has beyond fresh players does not
        ///     count here: veterans are faster, and may be.
        /// </summary>
        public static double Heads(IReadOnlyList<double> strengths)
        {
            if (strengths == null) return 0;
            var heads = 0.0;
            foreach (var s in strengths) heads += CombatMath.Clamp(s, TowerPressure.MinHead, 1);
            return heads;
        }

        /// <summary>
        ///     The time a group needs for a part's objectives, as a share of the suggested group's: what the weights
        ///     aim for. 1.4 for two heads, 1 for five, 0.84 for ten, 0.63 for thirty.
        /// </summary>
        public static double TimeShare(double heads)
        {
            var p = Math.Max(MinHeads, heads) / Tower.SuggestedGroup;
            return Math.Pow(p, p < 1 ? -SmallGroupExponent : -BigGroupExponent);
        }

        /// <summary>
        ///     How fast a group defeats a part's monsters, as a share of the suggested group's: its heads, over what a
        ///     kill takes them. The walk stays, the fight follows the monsters' health, which follows the group
        ///     (<see cref="TowerPressure.MonsterHealth"/>: the floor's bite over a full group, its mercy under one).
        ///     The simulator's own count agrees within a few hundredths on every floor.
        /// </summary>
        public static double KillRate(int floor, double heads)
        {
            var p = Math.Max(MinHeads, heads) / Tower.SuggestedGroup;
            return p / (1 - FightShare + FightShare * TowerPressure.MonsterHealth(floor, p));
        }

        /// <summary>
        ///     What one counted kill on a floor's part is worth with <paramref name="heads"/> on the part: 1 for the
        ///     suggested group, so that the part's objectives take the group <see cref="TimeShare"/> of that group's
        ///     time. More heads always make more progress a minute (it goes as 1 / TimeShare, which only grows), so
        ///     nobody gains by leaving a part and nobody slows it by joining.
        /// </summary>
        public static double KillWeight(int floor, double heads) => 1 / (KillRate(floor, heads) * TimeShare(heads));

        /// <summary>
        ///     What one counted gathering round is worth with <paramref name="gatherers"/> of the stream's players
        ///     gathering for the objective: 1 for <see cref="SuggestedGatherers"/>, more for a lone gatherer, less for
        ///     a crowd, by the same curve (a gatherer's rounds do not slow down in a crowd, so there is nothing else
        ///     to it): one gatherer 1.55, four 0.60, twelve 0.27.
        /// </summary>
        public static double GatherWeight(double gatherers)
        {
            var heads = Tower.SuggestedGroup * Math.Max(1, gatherers) / SuggestedGatherers;
            return 1 / (heads / Tower.SuggestedGroup * TimeShare(heads));
        }
    }
}

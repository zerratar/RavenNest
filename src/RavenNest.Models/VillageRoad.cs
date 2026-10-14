using System;

namespace RavenNest.Models
{
    /// <summary>A road in a village, as the game client draws it.</summary>
    public class VillageRoadInfo
    {
        public Guid Id { get; set; }
        public int Type { get; set; }
        /// <summary>The centre line as x, z pairs: x0, z0, x1, z1, ...</summary>
        public double[] Points { get; set; }
        /// <summary>The width at each point as a multiple of the type's width, or null for 1 everywhere.</summary>
        public double[] Widths { get; set; }
        /// <summary>How round each end is: 1 a half circle, 0 a square end.</summary>
        public double StartRound { get; set; } = 1;
        public double EndRound { get; set; } = 1;
    }

    /// <summary>A kind of road the village can build, and what a metre of it costs.</summary>
    public class VillageRoadType
    {
        public int Id { get; set; }
        public string Name { get; set; }
        /// <summary>Width of the road, in metres.</summary>
        public double Width { get; set; }
        /// <summary>The town hall tier it unlocks at, 0 to 4 (shown in game as 1 to 5).</summary>
        public int Tier { get; set; }
        /// <summary>Whether this village has the tier for it.</summary>
        public bool Unlocked { get; set; }
        public double Wood { get; set; }
        public double Ore { get; set; }
        public double Fish { get; set; }
        public double Wheat { get; set; }
        public double Coins { get; set; }
    }

    /// <summary>
    /// A road to build or change: its type and centre line, and optionally a width per point and the
    /// shape of its ends. A client that sends neither gets 1 everywhere and round ends.
    /// </summary>
    public class VillageRoadRequest
    {
        public int Type { get; set; }
        public double[] Points { get; set; }
        public double[] Widths { get; set; }
        public double? StartRound { get; set; }
        public double? EndRound { get; set; }
    }

    /// <summary>
    /// What came of building, changing or removing a road, and the village's resources after it,
    /// so the village bar can show the spend at once.
    /// </summary>
    public class VillageRoadResult
    {
        public bool Ok { get; set; }
        /// <summary>Why not, when not: locked, resources, invalid, unknown.</summary>
        public string Error { get; set; }
        public VillageRoadInfo Road { get; set; }
        public double Wood { get; set; }
        public double Ore { get; set; }
        public double Fish { get; set; }
        public double Wheat { get; set; }
        public double Coins { get; set; }
    }
}

using System;

namespace RavenNest.Models
{
    /// <summary>A kind of decor a village can place, what it costs and whether this village has it.</summary>
    public class VillageDecorPiece
    {
        public string Key { get; set; }
        public string Name { get; set; }
        public string Group { get; set; }

        /// <summary>Flowers, grass, small stones: hidden under roads and buildings, never in the way.</summary>
        public bool Small { get; set; }

        /// <summary>The town hall tier it unlocks at, 0 to 4 (shown in game as 1 to 5).</summary>
        public int Tier { get; set; }
        public bool Unlocked { get; set; }
        public double Wood { get; set; }
        public double Ore { get; set; }
        public double Fish { get; set; }
        public double Wheat { get; set; }
        public double Coins { get; set; }
    }

    /// <summary>
    /// A change to the village's decor: a placed piece (DefaultKey empty), or one of the island's own pieces
    /// that was moved (its new place) or removed.
    /// </summary>
    public class VillageDecorInfo
    {
        public Guid Id { get; set; }
        public string Piece { get; set; }
        public string DefaultKey { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public double Rotation { get; set; }
        public double Scale { get; set; } = 1;
        public bool Removed { get; set; }
    }

    /// <summary>
    /// Placing a piece (Piece and the place), or moving or removing one: a placed piece by Id, an island piece by
    /// DefaultKey.
    /// </summary>
    public class VillageDecorRequest
    {
        public string Piece { get; set; }
        public Guid? Id { get; set; }
        public string DefaultKey { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public double Rotation { get; set; }
        public double Scale { get; set; } = 1;
    }

    /// <summary>What came of it, and the village's resources after it, so the village bar shows the spend at once.</summary>
    public class VillageDecorResult
    {
        public bool Ok { get; set; }

        /// <summary>Why not, when not: locked, resources, invalid, unknown, removed.</summary>
        public string Error { get; set; }
        public VillageDecorInfo Decor { get; set; }

        /// <summary>What removing gave back.</summary>
        public double RefundWood { get; set; }
        public double RefundOre { get; set; }
        public double RefundFish { get; set; }
        public double RefundWheat { get; set; }
        public double RefundCoins { get; set; }

        public double Wood { get; set; }
        public double Ore { get; set; }
        public double Fish { get; set; }
        public double Wheat { get; set; }
        public double Coins { get; set; }
    }
}

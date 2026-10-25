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

        /// <summary>The village level it needs as well, 0 for none.</summary>
        public int Level { get; set; }

        /// <summary>The seasonal event that unlocks it (christmas, halloween), for a village that streams during it; or null.</summary>
        public string Event { get; set; }

        /// <summary>The coins it takes from the village treasury, once, to unlock it (the Special pieces); 0 for none.</summary>
        public double UnlockCoins { get; set; }

        /// <summary>The quest chain that unlocks it for the village where someone finishes it ("cat"), or null.</summary>
        public string Quest { get; set; }

        /// <summary>What a viewer pays in loyalty points to give the village one (!decor gift).</summary>
        public long Points { get; set; }

        /// <summary>Viewers' gifts of this piece waiting to be placed, and the last givers' names.</summary>
        public long Gifts { get; set; }
        public string GiftedBy { get; set; }
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

        /// <summary>A viewer's gift: removing it puts it back with the gifts.</summary>
        public bool Gift { get; set; }
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

        /// <summary>Placing: one of the viewers' gifts of this piece, free.</summary>
        public bool Gift { get; set; }
    }

    /// <summary>What came of it, and the village's resources after it, so the village bar shows the spend at once.</summary>
    public class VillageDecorResult
    {
        public bool Ok { get; set; }

        /// <summary>Why not, when not: locked, resources, invalid, unknown, removed, nogift, unlocked.</summary>
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

    /// <summary>A viewer gives the streamer's village a decor piece with their loyalty points (!decor gift).</summary>
    public class VillageDecorGiftRequest
    {
        /// <summary>The piece's key; the game finds it from what the viewer typed.</summary>
        public string Piece { get; set; }

        /// <summary>The viewer, as the chat knows them.</summary>
        public string Platform { get; set; }
        public string PlatformId { get; set; }
        public string UserName { get; set; }
    }

    public class VillageDecorGiftResult
    {
        public bool Ok { get; set; }

        /// <summary>Why not: unknown (piece), locked, nouser, points, invalid.</summary>
        public string Error { get; set; }
        public string Piece { get; set; }
        public string PieceName { get; set; }
        public long Points { get; set; }

        /// <summary>The viewer's loyalty points with this streamer, after the gift (or now, when refused).</summary>
        public long PointsLeft { get; set; }

        /// <summary>How many of the piece wait to be placed now.</summary>
        public long Gifts { get; set; }
    }
}

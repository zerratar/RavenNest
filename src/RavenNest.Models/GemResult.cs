namespace RavenNest.Models
{
    /// <summary>What a gem command did (docs/gems-design.md in the game repo). Kinds and tiers are RavenNest.Combat's GemKind and GemTier.</summary>
    public enum GemResultStatus
    {
        Ok,
        Error,
        NotInClan,
        NoEnchanting,
        NoRawGem,
        NoGem,
        NotEnoughGems,
        NotEnoughCoins,
        HighestTier,
        NoSuchItem,
        NotSocketable,
        NoSockets,
        SocketsFull,
        EmptySocket,
        NoSuchSocket,
        Busy,
    }

    public class GemResult
    {
        public GemResultStatus Status { get; set; }
        public int Kind { get; set; }
        public int Tier { get; set; }

        /// <summary>Enchanting: gems made per tier (Chipped first). Combining: [made].</summary>
        public int[] Counts { get; set; }

        /// <summary>The coins it cost (combining).</summary>
        public long Coins { get; set; }

        /// <summary>The item's display name (socketing).</summary>
        public string ItemName { get; set; }

        /// <summary>The socket used (1 based), and how many the item has.</summary>
        public int Socket { get; set; }
        public int SocketCount { get; set; }

        /// <summary>A gem taken out (or socketed over): which, the chance it had, and whether it came back.</summary>
        public int? OldKind { get; set; }
        public int? OldTier { get; set; }
        public double KeepChance { get; set; }
        public bool Kept { get; set; }

        public static GemResult Fail(GemResultStatus status) => new GemResult { Status = status };
    }
}

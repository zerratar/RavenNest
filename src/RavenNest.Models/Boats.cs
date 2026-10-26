namespace RavenNest.Models
{
    /// <summary>!boat build: a boat from the shipwright on Away, built from crafted parts and paid for in coins.</summary>
    public class BoatBuildRequest
    {
        public string Boat { get; set; }
    }

    public class BoatPart
    {
        public string Item { get; set; }
        public long Amount { get; set; }
        public long Have { get; set; }
    }

    /// <summary>What the shipwright builds, and what each boat asks for.</summary>
    public class BoatBlueprint
    {
        public string Boat { get; set; }
        public int Sailing { get; set; }
        public int Fishing { get; set; }
        public int Crafting { get; set; }
        public long Fee { get; set; }
        public BoatPart[] Parts { get; set; }
    }

    public class BoatBuildResult
    {
        public bool Ok { get; set; }

        /// <summary>Why not: notinsession, unknown (boat), island (not on Away), level, parts, coins, failed.</summary>
        public string Error { get; set; }
        public string Boat { get; set; }
        public string Island { get; set; }

        /// <summary>For level: the skill and the level it needs.</summary>
        public string Skill { get; set; }
        public int Level { get; set; }
        public long Fee { get; set; }

        /// <summary>The character's coins after building, or now when refused.</summary>
        public long Coins { get; set; }

        /// <summary>For parts: what is still missing.</summary>
        public BoatPart[] Missing { get; set; }

        /// <summary>What the shipwright builds (for unknown, and for !boat with nothing more).</summary>
        public BoatBlueprint[] Blueprints { get; set; }
    }
}

using System;

namespace RavenNest.Models
{
    /// <summary>What the Mangrove island paid one character: a treasure or pirates' chest, or a regatta prize.</summary>
    public class MangroveReward
    {
        public Guid CharacterId { get; set; }
        public long Coins { get; set; }
        public EventItemReward[] Items { get; set; }
        /// <summary>Boat looks earned with it (docs/own-boats.md in the game): "sails_9", "flag_12".</summary>
        public string[] Looks { get; set; }
        /// <summary>A decor piece the village unlocked with it (set on the first reward only).</summary>
        public string Decor { get; set; }
    }
}

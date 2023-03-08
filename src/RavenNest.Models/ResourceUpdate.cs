using System;

namespace RavenNest.Models
{
    public class ResourceUpdate
    {
        [Obsolete("Use CharacterId instead.")]
        public string UserId { get; set; }
        public System.Guid CharacterId { get; set; }
        public double WoodAmount { get; set; }
        public double OreAmount { get; set; }
        public double WheatAmount { get; set; }
        public double FishAmount { get; set; }
        public double CoinsAmount { get; set; }
    }
}

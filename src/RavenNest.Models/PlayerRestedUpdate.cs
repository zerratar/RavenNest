using System;

namespace RavenNest.Models
{
    public class PlayerRestedUpdate
    {
        public Guid CharacterId { get; set; }
        public double ExpBoost { get; set; }
        public double StatsBoost { get; set; }
        public double RestedPercent { get; set; }
        public double RestedTime { get; set; }
    }
}

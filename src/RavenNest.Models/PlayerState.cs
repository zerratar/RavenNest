using System;

namespace RavenNest.Models
{
    public class PlayerState
    {
        public Guid CharacterId { get; set; }
        public string CurrentTask { get; set; }
        public int[] Level { get; set; }
        public double[] Experience { get; set; }
        public double[] Statistics { get; set; }
        public float SyncTime { get; set; }
        public int Revision { get; set; }
    }
}

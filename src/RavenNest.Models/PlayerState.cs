﻿using System;

namespace RavenNest.Models
{
    public class PlayerState
    {
        public string UserId { get; set; }
        public Guid CharacterId { get; set; }
        public string CurrentTask { get; set; }
        public int[] Level { get; set; }
        public decimal[] Experience { get; set; }
        public decimal[] Statistics { get; set; }
        public float SyncTime { get; set; }
        public int Revision { get; set; }
    }
}

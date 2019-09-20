using System;

namespace RavenNest.Models
{
    public class GameInfo
    {
        public string UserId { get; set; }
        public TimeSpan Uptime { get; set; }
        public int PeakPlayerCount { get; set; }
        public int PlayerCount { get; set; }
        public int EventRevision { get; set; }
    }
}
using System;
using System.Collections.Generic;

namespace RavenNest.BusinessLogic.Data
{
    public class BotStats
    {
        public int CommandsPerSecondsMax { get; set; }
        public int JoinedChannelsCount { get; set; }
        public int UserCount { get; set; }
        public int ConnectionCount { get; set; }
        public int SessionCount { get; set; }

        public long TotalCommandCount { get; set; }
        public double CommandsPerSecondsDelta { get; set; }

        public TimeSpan Uptime { get; set; }
        public DateTime LastSessionStarted { get; set; }
        public DateTime LastSessionEnded { get; set; }
        public DateTime Started { get; set; }

        public DateTime LastUpdated { get; set; }
        public Dictionary<string, object> Values { get; set; } = new Dictionary<string, object>();
        public TimeSpan TimeSinceUpdate => DateTime.UtcNow - LastUpdated;
    }
}

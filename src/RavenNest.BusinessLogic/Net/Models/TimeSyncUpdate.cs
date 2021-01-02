using System;

namespace RavenNest.BusinessLogic.Net
{
    public class TimeSyncUpdate
    {
        public TimeSpan Delta { get; set; }
        public DateTime LocalTime { get; set; }
        public DateTime ServerTime { get; set; }
    }
}

using System;

namespace RavenNest.Sessions
{
    public class StreamerInfo
    {
        public string StreamerUserId { get; set; }
        public string StreamerUserName { get; set; }
        public Guid? StreamerSessionId { get; set; }
        public bool IsRavenfallRunning { get; set; }
        public int PlayerCount { get; set; }
        public DateTime? Started { get; set; }
        public string ClientVersion { get; set; }
        public Guid? JoinedCharacterId { get; set; }
    }
}

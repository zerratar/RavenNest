using System;

namespace RavenNest.Models
{
    public class GameUpdatedRequest
    {
        public string ExpectedVersion { get; set; }
        public bool UpdateRequired { get; set; }
    }

    public class PlayerUnstuckMessage
    {
        public Guid[] Ids { get; set; }
    }

    public class PlayerTeleportMessage
    {
        public Guid[] Ids { get; set; }
        public Island Island { get; set; }
    }
}

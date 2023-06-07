using System;
using System.Collections.Generic;

namespace RavenNest.Models
{
    public class PlayerAdd
    {
        public string UserName { get; set; }
        public string Identifier { get; set; }
        public Guid UserId { get; set; }
        public Guid CharacterId { get; set; }
        public string PlatformId { get; set; }
        public string Platform { get; set; }
    }

    public class StreamRaidInfo
    {
        public string RaiderUserName { get; set; }
        public Guid RaiderUserId { get; set; }
        public List<StreamRaidPlayer> Players { get; set; }
    }

    public class StreamRaidPlayer
    {
        public Guid UserId { get; set; }
        public Guid CharacterId { get; set; }
        public string Username { get; set; }
    }
}

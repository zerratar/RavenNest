using System;

namespace RavenNest.Sessions
{
    public class SessionInfo
    {
        public Guid Id { get; set; }
        public string UserId { get; set; }
        public string UserName { get; set; }
        public bool Authenticated { get; set; }
        public bool Moderator { get; set; }
        public bool Administrator { get; set; }
        public bool RequiresPasswordChange { get; set; }
        public int Tier { get; set; }
    }
}

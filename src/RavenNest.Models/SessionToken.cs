using System;

namespace RavenNest.Models
{
    public class SessionToken
    {
        public Guid UserId { get; set; }
        public Guid SessionId { get; set; }
        public DateTime StartedUtc { get; set; }
        public DateTime ExpiresUtc { get; set; }
        public string AuthToken { get; set; }
        public bool Expired => DateTime.UtcNow >= ExpiresUtc;
        public string UserName { get; set; }
        public string DisplayName { get; set; }

        [Obsolete("Use UserId instead.")]
        public string TwitchUserId { get; set; }

        [Obsolete("Use UserName instead.")]
        public string TwitchUserName { get; set; }
        [Obsolete("Use DisplayName instead.")]
        public string TwitchDisplayName { get; set; }
        public string ClientVersion { get; set; }
    }
}

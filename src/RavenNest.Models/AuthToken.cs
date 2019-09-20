using System;

namespace RavenNest.Models
{
    public class AuthToken
    {
        public Guid UserId { get; set; }
        public DateTime IssuedUtc { get; set; }
        public DateTime ExpiresUtc { get; set; }
        public string Token { get; set; }
        public bool Expired => DateTime.UtcNow >= ExpiresUtc;
    }
}

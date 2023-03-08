using System;

namespace RavenNest.Models
{
    public class PlatformIdentity
    {
        public string UserId { get; set; }
        public string Platform { get; set; }
    }

    public class PlayerId
    {
        public Guid Id { get; set; }
    }
}

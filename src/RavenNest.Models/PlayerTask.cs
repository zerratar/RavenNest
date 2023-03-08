using System;

namespace RavenNest.Models
{
    public class PlayerTask
    {
        [Obsolete("Use CharacterId instead")]
        public string UserId { get; set; }
        public Guid CharacterId { get; set; }
        public string Task { get; set; }
        public string TaskArgument { get; set; }
    }
}

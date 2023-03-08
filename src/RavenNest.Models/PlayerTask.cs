using System;

namespace RavenNest.Models
{
    public class PlayerTask
    {
        public Guid PlayerId { get; set; }
        public string Task { get; set; }
        public string TaskArgument { get; set; }
    }
}

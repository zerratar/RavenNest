using System;

namespace RavenNest.Models
{
    public class PlayerNameUpdate
    {
        public Guid PlayerId { get; set; }
        public string Name { get; set; }
    }
}

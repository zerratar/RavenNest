using System;

namespace RavenNest.Models
{
    public class PlayerExpUpdate
    {
        public Guid PlayerId { get; set; }
        public string Skill { get; set; }
        public double Experience { get; set; }
    }
}

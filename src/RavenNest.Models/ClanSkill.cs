using System;

namespace RavenNest.Models
{
    public class ClanSkill
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public int Level { get; set; }
        public double Experience { get; set; }
        public int MaxLevel { get; set; }
    }
}

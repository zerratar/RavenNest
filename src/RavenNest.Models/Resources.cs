using System;

namespace RavenNest.Models
{
    public class Resources
    {
        public Guid Id { get; set; }
        public double Wood { get; set; }
        public double Ore { get; set; }
        public double Fish { get; set; }
        public double Wheat { get; set; }
        public double Magic { get; set; }
        public double Arrows { get; set; }
        public double Coins { get; set; }
        public int Revision { get; set; }
    }
}

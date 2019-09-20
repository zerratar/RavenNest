using System;

namespace RavenNest.Models
{
    public class Resources
    {
        public Guid Id { get; set; }
        public decimal Wood { get; set; }
        public decimal Ore { get; set; }
        public decimal Fish { get; set; }
        public decimal Wheat { get; set; }
        public decimal Magic { get; set; }
        public decimal Arrows { get; set; }
        public decimal Coins { get; set; }
        public int Revision { get; set; }
    }
}
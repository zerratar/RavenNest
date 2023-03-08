using System;

namespace RavenNest.Models
{
    public class PlayerMove
    {
        public Guid PlayerId { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }
    }
}

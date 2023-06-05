using System;

namespace RavenNest.Models
{
    public class GameEvent
    {
        public Guid GameSessionId { get; set; }
        public int Type { get; set; }
        public int Revision { get; set; }
        public byte[] Data { get; set; }
    }
}

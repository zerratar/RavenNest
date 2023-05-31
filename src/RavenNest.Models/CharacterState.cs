using System;

namespace RavenNest.Models
{
    public class CharacterState
    {
        public Guid Id { get; set; }
        public int Health { get; set; }
        public bool InRaid { get; set; }
        public bool InArena { get; set; }
        public bool InDungeon { get; set; }
        public bool InOnsen { get; set; }
        public string Task { get; set; }
        public string TaskArgument { get; set; }
        public string Island { get; set; }
        public double? X { get; set; }
        public double? Y { get; set; }
        public double? Z { get; set; }
        public double RestedTime { get; set; }
    }
}

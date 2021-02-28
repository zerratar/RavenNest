using System;

namespace RavenNest.Models
{
    public class CharacterState
    {
        public Guid Id { get; set; }
        public int Health { get; set; }
        public string DuelOpponent { get; set; }
        public bool InRaid { get; set; }
        public bool InArena { get; set; }
        public bool InDungeon { get; set; }
        public bool InOnsen { get; set; }
        public string Task { get; set; }
        public string TaskArgument { get; set; }
        public string Island { get; set; }
        public decimal? X { get; set; }
        public decimal? Y { get; set; }
        public decimal? Z { get; set; }
        public decimal RestedTime { get; set; }
    }
}

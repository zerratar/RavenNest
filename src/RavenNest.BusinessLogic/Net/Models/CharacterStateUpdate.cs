using System;

namespace RavenNest.BusinessLogic.Net
{
    public class CharacterStateUpdate
    {
        public Guid CharacterId { get; set; }
        public int Health { get; set; }
        public string Island { get; set; }
        public string DuelOpponent { get; set; }
        public bool InRaid { get; set; }
        public bool InArena { get; set; }
        public bool InDungeon { get; set; }
        public bool InOnsen { get; set; }
        public string Task { get; set; }
        public string TaskArgument { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
    }
}

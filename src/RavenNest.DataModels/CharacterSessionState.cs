using System;

namespace RavenNest.DataModels
{
    public class CharacterSessionState
    {
        public Guid SessionId { get; set; }
        public Guid CharacterId { get; set; }        
        public DateTime LastTaskUpdate { get; set; }
    }
}
using System;

namespace RavenNest.Models
{
    public class GameSessionPlayer
    {
        public Guid CharacterId { get; set; }
        public string TwitchUserId { get; set; }
        public string UserName { get; set; }
        public bool IsAdmin { get; set; }
        public bool IsModerator { get; set; }
        public DateTime LastExpUpdate { get; set; }
        public DateTime LastExpSaveRequest { get; set; }
        public DateTime LastStateUpdate { get; set; }
        public DateTime LastStateSaveRequest { get; set; }
    }
}

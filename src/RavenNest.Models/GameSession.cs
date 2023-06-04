using System;
using System.Collections.Generic;

namespace RavenNest.Models
{
    public class GameSession
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string TwitchUserId { get; set; }
        public string UserName { get; set; }
        public bool AdminPrivileges { get; set; }
        public bool ModPrivileges { get; set; }
        public List<GameSessionPlayer> Players { get; set; }
        public DateTime Started { get; set; }
        public DateTime? Updated { get; set; }
        public TimeSpan? AvgSaveTime { get; set; }
        public string ClientVersion { get; set; }
        public float SyncTime { get; set; }
        public int Status { get; set; }
    }
}

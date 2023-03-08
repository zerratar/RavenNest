using System;
using System.Collections.Generic;
using RavenNest.DataModels;
using RavenNest.Models;

namespace RavenNest
{
    public class SessionInfo
    {
        public Guid Id { get; set; }
        public string SessionId { get; set; }
        public string TwitchUserId { get; set; }
        public string Platform { get; set; }
        public Guid UserId { get; set; }
        public string UserName { get; set; }
        public bool Authenticated { get; set; }
        public bool Moderator { get; set; }
        public bool Administrator { get; set; }
        public bool RequiresPasswordChange { get; set; }
        public bool CanChangeClanName { get; set; }
        public int Tier { get; set; }
        public List<CharacterGameSession> PlaySessions { get; set; }
        public List<AuthServiceConnection> Connections { get; set; }
        public Guid? ActiveCharacterId { get; set; }
        public bool AcceptedCookiesDisclaimer { get; set; }
        public bool UserNameChanged { get; set; }
        public AuthToken AuthToken { get; set; }
        public bool Extension { get; set; }
        public UserPatreon Patreon { get; set; }
    }
}

using RavenNest.Models;
using System;
using System.Collections.Generic;

namespace RavenNest.Sessions
{
    public class SessionInfo
    {
        public Guid Id { get; set; }
        public string SessionId { get; set; }
        public string UserId { get; set; }
        public string UserName { get; set; }
        public bool Authenticated { get; set; }
        public bool Moderator { get; set; }
        public bool Administrator { get; set; }
        public bool RequiresPasswordChange { get; set; }
        public bool CanChangeClanName { get; set; }
        public int Tier { get; set; }
        public List<CharacterGameSession> PlaySessions { get; set; }
        public Guid? ActiveCharacterId { get; set; }
        public bool AcceptedCookiesDisclaimer { get; set; }
        public bool UserNameChanged { get; set; }
        public AuthToken AuthToken { get; set; }
    }

    public class CharacterGameSession
    {
        public Guid CharacterId { get; set; }
        public string CharacterName { get; set; }
        public int CharacterIndex { get; set; }
        public int CharacterCombatLevel { get; set; }
        public string SessionTwitchUserId { get; set; }
        public string SessionTwitchUserName { get; set; }
        public DateTime Joined { get; set; }
    }
}

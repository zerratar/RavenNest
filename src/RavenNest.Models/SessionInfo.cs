using System;
using System.Collections.Generic;

namespace RavenNest.Models
{
    public class SessionInfo
    {
        public Guid Id { get; set; }
        public string SessionId { get; set; }
        [Obsolete("Use Connections instead.")]
        public string TwitchUserId { get; set; }
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
        public bool UserNameChanged { get; set; }
        public AuthToken AuthToken { get; set; }
        public bool Extension { get; set; }
        public PatreonInfo Patreon { get; set; }
    }

    public class PatreonInfo
    {
        public Guid? UserId { get; set; }
        public string TwitchUserId { get; set; }
        public string PledgeTitle { get; set; }
        public string FirstName { get; set; }
        public string FullName { get; set; }
        public long? PatreonId { get; set; }
        public long? PledgeAmount { get; set; }
        public string Email { get; set; }
        public int? Tier { get; set; }
        public string ProfilePicture { get; set; }
        public DateTime? Updated { get; set; }
        public DateTime? Created { get; set; }
    }
}

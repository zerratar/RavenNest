using RavenNest.DataAnnotations;
using System;

namespace RavenNest.DataModels
{
    public partial class CharacterClanInvite : Entity<CharacterClanInvite>
    {
        [PersistentData] private Guid characterId;
        [PersistentData] private Guid clanId;
        [PersistentData] private Guid? inviterUserId;
        [PersistentData] private Guid? notificationId;
        [PersistentData] private DateTime created;
    }
}

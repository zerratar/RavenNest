using RavenNest.DataAnnotations;
using System;

namespace RavenNest.DataModels
{
    public partial class CharacterClanMembership : Entity<CharacterClanMembership>
    {
        [PersistentData] private Guid characterId;
        [PersistentData] private Guid clanId;
        [PersistentData] private Guid clanRoleId;
        [PersistentData] private DateTime joined;
    }
}

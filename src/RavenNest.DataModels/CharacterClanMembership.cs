using System;

namespace RavenNest.DataModels
{
    public partial class CharacterClanMembership : Entity<CharacterClanMembership>
    {
        private Guid id; public Guid Id { get => id; set => Set(ref id, value); }
        private Guid characterId; public Guid CharacterId { get => characterId; set => Set(ref characterId, value); }
        private Guid clanId; public Guid ClanId { get => clanId; set => Set(ref clanId, value); }
        private Guid clanRoleId; public Guid ClanRoleId { get => clanRoleId; set => Set(ref clanRoleId, value); }
        private DateTime joined; public DateTime Joined { get => joined; set => Set(ref joined, value); }
    }
}

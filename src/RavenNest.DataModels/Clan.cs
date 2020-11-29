using System;

namespace RavenNest.DataModels
{
    public partial class Clan : Entity<Clan>
    {
        private Guid id; public Guid Id { get => id; set => Set(ref id, value); }
        private Guid userId; public Guid UserId { get => userId; set => Set(ref userId, value); }
        private string name; public string Name { get => name; set => Set(ref name, value); }
        private string logo; public string Logo { get => logo; set => Set(ref logo, value); }
        private DateTime created; public DateTime Created { get => created; set => Set(ref created, value); }
    }

    public partial class ClanRole : Entity<ClanRole>
    {
        private Guid id; public Guid Id { get => id; set => Set(ref id, value); }
        private Guid clanId; public Guid ClanId { get => clanId; set => Set(ref clanId, value); }
        private int cape; public int Cape { get => cape; set => Set(ref cape, value); }
        private int level; public int Level { get => level; set => Set(ref level, value); }
        private string name; public string Name { get => name; set => Set(ref name, value); }
    }

    public partial class CharacterClanMembership : Entity<CharacterClanMembership>
    {
        private Guid id; public Guid Id { get => id; set => Set(ref id, value); }
        private Guid characterId; public Guid CharacterId { get => characterId; set => Set(ref characterId, value); }
        private Guid clanId; public Guid ClanId { get => clanId; set => Set(ref clanId, value); }
        private Guid clanRoleId; public Guid ClanRoleId { get => clanRoleId; set => Set(ref clanRoleId, value); }
        private DateTime joined; public DateTime Joined { get => joined; set => Set(ref joined, value); }
    }

    public partial class CharacterClanInvite : Entity<CharacterClanInvite>
    {
        private Guid id; public Guid Id { get => id; set => Set(ref id, value); }
        private Guid characterId; public Guid CharacterId { get => characterId; set => Set(ref characterId, value); }
        private Guid clanId; public Guid ClanId { get => clanId; set => Set(ref clanId, value); }
        private Guid? inviterUserId; public Guid? InviterUserId { get => inviterUserId; set => Set(ref inviterUserId, value); }
        private Guid? notificationId; public Guid? NotificationId { get => notificationId; set => Set(ref notificationId, value); }
        private DateTime created; public DateTime Created { get => created; set => Set(ref created, value); }
    }
}

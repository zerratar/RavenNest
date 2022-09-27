using System;

namespace RavenNest.DataModels
{
    public partial class CharacterClanInvite : Entity<CharacterClanInvite>
    {
        private Guid characterId; public Guid CharacterId { get => characterId; set => Set(ref characterId, value); }
        private Guid clanId; public Guid ClanId { get => clanId; set => Set(ref clanId, value); }
        private Guid? inviterUserId; public Guid? InviterUserId { get => inviterUserId; set => Set(ref inviterUserId, value); }
        private Guid? notificationId; public Guid? NotificationId { get => notificationId; set => Set(ref notificationId, value); }
        private DateTime created; public DateTime Created { get => created; set => Set(ref created, value); }
    }
}

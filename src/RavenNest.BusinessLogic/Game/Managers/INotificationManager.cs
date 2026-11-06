using RavenNest.DataModels;
using System;
using System.Collections.Generic;

namespace RavenNest.BusinessLogic.Game
{
    public interface INotificationManager
    {
        IReadOnlyList<UserNotification> GetNotifications(Guid id, int take = int.MaxValue);
        UserNotification ClanInviteReceived(Guid clanId, Guid characterId, Guid? senderUserId);
        void ClanInviteAccepted(Guid clanId, Guid characterId, DateTime utcNow, Guid? inviterUserId);
        void ClanJoinRequestReceived(ClanJoinRequest request, IReadOnlyList<Guid> deciderUserIds);
        void ClanJoinRequestDecided(ClanJoinRequest request, bool accepted);
        void DeleteNotification(Guid notificationId);
    }
}

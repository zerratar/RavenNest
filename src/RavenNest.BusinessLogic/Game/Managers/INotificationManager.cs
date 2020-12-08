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
        void DeleteNotification(Guid notificationId);
    }
}

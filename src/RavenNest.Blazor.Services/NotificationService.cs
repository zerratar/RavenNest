using Microsoft.AspNetCore.Http;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Game;
using RavenNest.DataModels;
using RavenNest.Sessions;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RavenNest.Blazor.Services
{
    public class NotificationService : RavenNestService
    {
        private readonly GameData gameData;
        private readonly INotificationManager notificationManager;
        public NotificationService(
            GameData gameData,
            INotificationManager notificationManager,
            IHttpContextAccessor accessor,
            ISessionInfoProvider sessionInfoProvider)
            : base(accessor, sessionInfoProvider)
        {
            this.gameData = gameData;
            this.notificationManager = notificationManager;
        }

        public async Task<IReadOnlyList<UserNotification>> DeleteNotificationAsync(string twitchUserId, Guid notificationId)
        {
            var userId = gameData.GetUserByTwitchId(twitchUserId)?.Id;
            if (userId == null)
                return new List<UserNotification>();

            return await Task.Run(() =>
            {
                notificationManager.DeleteNotification(notificationId);
                return notificationManager.GetNotifications(userId.GetValueOrDefault());
            });
        }

        public async Task<IReadOnlyList<UserNotification>> GetNotificationsAsync(string twitchUserId, int take = int.MaxValue)
        {
            var userId = gameData.GetUserByTwitchId(twitchUserId)?.Id;
            if (userId == null)
                return new List<UserNotification>();

            return await Task.Run(() =>
            {
                return notificationManager.GetNotifications(userId.GetValueOrDefault(), take);
            });
        }
    }
}

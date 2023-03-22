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
            SessionInfoProvider sessionInfoProvider)
            : base(accessor, sessionInfoProvider)
        {
            this.gameData = gameData;
            this.notificationManager = notificationManager;
        }

        public async Task<IReadOnlyList<UserNotification>> DeleteNotificationAsync(Guid userId, Guid notificationId)
        {
            return await Task.Run(() =>
            {
                notificationManager.DeleteNotification(notificationId);
                return notificationManager.GetNotifications(userId);
            });
        }

        public async Task<IReadOnlyList<UserNotification>> GetNotificationsAsync(Guid userId, int take = int.MaxValue)
        {
            return await Task.Run(() =>
            {
                return notificationManager.GetNotifications(userId, take);
            });
        }
    }
}

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
        private readonly IGameData gameData;
        private readonly INotificationManager notificationManager;
        public NotificationService(
            IGameData gameData,
            INotificationManager notificationManager,
            IHttpContextAccessor accessor,
            ISessionInfoProvider sessionInfoProvider)
            : base(accessor, sessionInfoProvider)
        {
            this.gameData = gameData;
            this.notificationManager = notificationManager;
        }

        public async Task<IReadOnlyList<UserNotification>> GetNotificationsAsync(string twitchUserId)
        {
            var userId = gameData.GetUser(twitchUserId)?.Id;
            if (userId == null)
                return new List<UserNotification>();

            return await Task.Run(() =>
            {
                //var session = GetSession();
                //if (!session.Authenticated)
                //    return new List<UserNotification>();
                return notificationManager.GetNotifications(userId.GetValueOrDefault());
            });
        }
    }
}

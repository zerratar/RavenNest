using RavenNest.BusinessLogic.Data;
using RavenNest.DataModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RavenNest.BusinessLogic.Game
{
    public class NotificationManager : INotificationManager
    {
        private readonly IGameData gameData;

        public NotificationManager(IGameData gameData)
        {
            this.gameData = gameData;
        }

        public void ClanInviteAccepted(Guid clanId, Guid characterId, DateTime utcNow, Guid? inviterUserId)
        {
            var newMember = gameData.GetCharacter(characterId);
            if (newMember == null)
                return;

            var clan = gameData.GetClan(clanId);
            if (clan == null)
                return;

            var inviter = gameData.GetUser(inviterUserId.GetValueOrDefault());
            if (inviter == null)
                inviter = gameData.GetUser(clan.UserId);

            if (inviter == null)
                return;

            this.gameData.Add(new UserNotification
            {
                Id = Guid.NewGuid(),
                Title = $"{newMember.Name} has accepted your invite to {clan.Name}",
                Description = $"Welcome {newMember.Name} to {clan.Name}!",
                UserId = inviter.Id,
                RedirectUrl = "/clan",
                Time = DateTime.UtcNow
            });
        }

        public UserNotification ClanInviteReceived(Guid clanId, Guid characterId, Guid? inviterUserId)
        {
            var newMember = gameData.GetCharacter(characterId);
            if (newMember == null)
                return null;

            var clan = gameData.GetClan(clanId);
            if (clan == null)
                return null;

            var inviter = gameData.GetUser(inviterUserId.GetValueOrDefault());
            if (inviter == null)
                inviter = gameData.GetUser(clan.UserId);

            if (inviter == null)
                return null;

            var notification = new UserNotification
            {
                Id = Guid.NewGuid(),
                Title = $"You have been invited to join {clan.Name} by {inviter.UserName}",
                UserId = newMember.UserId,
                RedirectUrl = "/clan-invites",
                Time = DateTime.UtcNow
            };
            this.gameData.Add(notification);
            return notification;
        }

        public IReadOnlyList<UserNotification> GetNotifications(Guid id)
        {
            return gameData.GetNotifications(id).OrderByDescending(x => x.Time).ToList();
        }
    }
}

using Microsoft.AspNetCore.Http;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Game;
using RavenNest.Models;
using RavenNest.Sessions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RavenNest.Blazor.Services
{
    public class UserService : RavenNestService
    {
        private readonly IGameData gameData;
        private readonly IPlayerManager playerManager;

        public UserService(
            IGameData gameData,
            IPlayerManager playerManager,
            IHttpContextAccessor accessor,
            ISessionInfoProvider sessionInfoProvider)
            : base(accessor, sessionInfoProvider)
        {
            this.gameData = gameData;
            this.playerManager = playerManager;
        }

        public Task UpdateUserPatreonAsync(Guid userId, int patreonTier)
        {
            return Task.Run(() =>
            {
                var user = gameData.GetUser(userId);
                if (user == null)
                    return;

                user.PatreonTier = patreonTier;
            });
        }

        public async Task<IReadOnlyList<WebsiteAdminUser>> GetUsersByCreatedAsync(DateTime start, DateTime end)
        {
            return await Task.Run(() =>
            {
                var players = playerManager.GetFullPlayers();
                var users = GetUsers(players);
                return users.Where(x => x.Created >= start && x.Created <= end).ToList();
            });
        }

        public async Task<PagedCollection<WebsiteAdminUser>> GetUserPageAsync(string search, int pageIndex, int pageSize)
        {
            var players = await SearchForPlayersAsync(search);
            var pageItems = players.Skip(pageSize * pageIndex).Take(pageSize).ToList();
            return new PagedCollection<WebsiteAdminUser>(pageItems, players.Count);
        }

        public async Task<IReadOnlyList<WebsiteAdminUser>> SearchForPlayersAsync(string searchText)
        {
            return await Task.Run(() =>
            {
                var players = playerManager.GetFullPlayers();
                var users = GetUsers(players);
                if (string.IsNullOrEmpty(searchText))
                {
                    return users;
                }

                return users.Where(x =>
                    x.UserId.Contains(searchText, System.StringComparison.OrdinalIgnoreCase) ||
                    x.UserName.Contains(searchText, System.StringComparison.OrdinalIgnoreCase) ||
                    (x.Email != null && x.Email.Contains(searchText, System.StringComparison.OrdinalIgnoreCase)))
                .ToList();
            });
        }

        public async Task SetUserStatusAsync(Guid userId, AccountStatus status)
        {
            await Task.Run(() =>
            {
                var session = GetSession();
                if (!session.Authenticated || !session.Administrator)
                {
                    return;
                }

                var user = gameData.GetUser(userId);
                if (user == null)
                {
                    return;
                }

                user.Status = (int)status;
                if (status != AccountStatus.OK)
                {
                    playerManager.RemoveUserFromSessions(user);

                    var ownedSession = gameData.GetOwnedSessionByUserId(user.UserId);
                    if (ownedSession != null)
                    {
                        ownedSession.Status = (int)SessionStatus.Inactive;
                        ownedSession.Stopped = DateTime.UtcNow;
                    }
                }
            });
        }



        private List<WebsiteAdminUser> GetUsers(IEnumerable<WebsiteAdminPlayer> players)
        {
            var output = new Dictionary<string, WebsiteAdminUser>();
            var users = new Dictionary<string, DataModels.User>();
            foreach (var user in gameData.GetUsers())
            {
                // to ignore any potential collisions with ToDictionary with duplicated userId's
                users[user.UserId] = user;
            }

            foreach (var p in players)
            {
                if (!output.TryGetValue(p.UserId, out var user))
                {
                    if (!users.TryGetValue(p.UserId, out var u))
                        continue;

                    user = GetUser(u);
                    output[p.UserId] = user;
                }
                user.Characters.Add(p);
            }

            return output.Values.ToList();
        }

        private WebsiteAdminUser GetUser(DataModels.User userData)
        {
            return new WebsiteAdminUser
            {
                Characters = new List<WebsiteAdminPlayer>(),
                Created = userData.Created,
                Id = userData.Id,
                PatreonTier = userData.PatreonTier,
                UserId = userData.UserId,
                UserName = userData.UserName,
                Email = userData.Email,
                IsAdmin = userData.IsAdmin.GetValueOrDefault(),
                IsModerator = userData.IsModerator.GetValueOrDefault(),
                Status = userData.Status ?? 0
            };
        }
    }
}

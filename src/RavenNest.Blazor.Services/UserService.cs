using Microsoft.AspNetCore.Http;
using RavenNest.BusinessLogic;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Game;
using RavenNest.DataModels;
using RavenNest.Models;
using RavenNest.Sessions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
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

        public Task SetUserHiddenInHighscore(Guid userId, bool newValue)
        {
            return Task.Run(() =>
            {
                var user = gameData.GetUser(userId);
                if (user == null)
                    return;

                user.IsHiddenInHighscore = newValue;
            });
        }


        public Task UpdateUserRemarkAsync(Guid userId, string remark)
        {

            return Task.Run(() =>
            {
                var user = gameData.GetUser(userId);
                if (user == null)
                    return;

                gameData.SetUserProperty(userId, UserProperties.Comment, remark);
            });
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
                return users.AsList(x => x.Created >= start && x.Created <= end);
            });
        }

        public WebsiteAdminUser GetCurrentUser()
        {
            var session = GetSession();
            if (session == null) return null;
            return GetUser(session.AccountId);
        }

        public WebsiteAdminUser GetUser(Guid accountId)
        {
            return GetUser(gameData.GetUser(accountId));
        }

        public WebsiteAdminUser GetUser(string twitchUserId)
        {
            return GetUser(gameData.GetUserByTwitchId(twitchUserId));
        }

        public async Task<PagedCollection<WebsiteAdminUser>> GetUserPageAsync(string search, int pageIndex, int pageSize)
        {
            var players = await SearchForPlayersAsync(search);
            var pageItems = players.Slice(pageSize * pageIndex, pageSize);
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
                var result = new List<WebsiteAdminUser>();
                foreach (var x in users)
                {
                    if (Contains(x.UserId, searchText) || Contains(x.UserName, searchText) || Contains(x.Email, searchText))
                        result.Add(x);
                }
                return result;
            });
        }

        public async Task<IReadOnlyList<WebsiteAdminUser>> SearchForUserByUserOrPlayersLimitedAsync(string searchText, int pageSize)
        {
            return await Task.Run(() =>
            {
                var players = playerManager.GetFullPlayers();
                var users = GetUsers(players);
                if (string.IsNullOrEmpty(searchText))
                {
                    return users;
                }
                var result = new List<WebsiteAdminUser>();
                var count = 0;
                foreach (var x in users)
                {
                    var matchedCharacter = x.Characters.FirstOrDefault(c => c.UserName == searchText);
                    if (
                        Contains(x.UserId, searchText) ||
                        Contains(x.UserName, searchText) ||
                        Contains(x.Email, searchText))
                    {
                        result.Add(x);
                        count++;
                    }
                    else if (matchedCharacter != null)
                    {
                        result.Add(x);
                        count++;
                    }
                    if(count >= pageSize)
                    {
                        break;
                    }
                }

                return result;
            });
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool Contains(string a, string b)
        {
            if (a == null || b == null) return false;
            return a.IndexOf(b, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public async Task<bool> SetUserStatusAsync(Guid userId, AccountStatus status)
        {
            return await Task.Run(() =>
            {
                var session = GetSession();
                if (!session.Authenticated || !session.Administrator)
                {
                    return false;
                }

                var user = gameData.GetUser(userId);
                if (user == null)
                {
                    return false;
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

                return true;
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
            if (userData == null) return null;
            var clan = gameData.GetClanByUser(userData.Id);

            WebsiteClan websiteClan = null;
            if (clan != null)
            {
                websiteClan = new WebsiteClan
                {
                    Id = clan.Id,
                    CanChangeName = clan.CanChangeName,
                    NameChangeCount = clan.NameChangeCount,
                    Level = clan.Level,
                };
            }

            var bankItems = DataMapper.MapMany<RavenNest.Models.UserBankItem>(gameData.GetUserBankItems(userData.Id));
            var commentProperty = gameData.GetUserProperty(userData.Id, UserProperties.Comment);

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
                Status = userData.Status ?? 0,
                IsHiddenInHighscore = userData.IsHiddenInHighscore.GetValueOrDefault(),
                Clan = websiteClan,
                HasClan = websiteClan != null,
                Stash = bankItems,
                Comment = commentProperty
            };
        }
    }
}

using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Net;
using RavenNest.Models;

namespace RavenNest.BusinessLogic.Game
{
    public class SessionManager : ISessionManager
    {
        private readonly ITwitchClient twitchClient;
        private readonly IGameData gameData;
        private readonly IPlayerManager playerManager;

        private readonly int[] MaxMultiplier = new int[]
        {
            //0, 10, 15, 20
            0, 15, 30, 50
        };

        public SessionManager(
            ITwitchClient twitchClient,
            IGameData gameData,
            IPlayerManager playerManager)
        {
            this.twitchClient = twitchClient;
            this.gameData = gameData;
            this.playerManager = playerManager;
        }

        public async Task<SessionToken> BeginSessionAsync(
            AuthToken token,
            string clientVersion,
            string accessKey,
            bool isLocal,
            float syncTime)
        {
            var game = gameData.Client;
            var user = gameData.GetUser(token.UserId);

            if (game == null)
            {
                return null;
            }

            if (game.AccessKey != accessKey)
            {
                return null;
            }

            if (clientVersion.ToLower() != game.ClientVersion.ToLower())
            {
                return new SessionToken();
            }

            var userId = token.UserId;
            var activeSession = gameData.GetUserSession(userId);
            // x => x.UserId == userId && x.Status == (int)SessionStatus.Active

            var newGameSession = activeSession ?? gameData.CreateSession(userId);
            if (activeSession == null)
            {
                gameData.Add(newGameSession);
            }

            var activeChars = gameData.GetSessionCharacters(newGameSession);
            if (activeChars != null)
            {
                foreach (var c in activeChars)
                {
                    c.UserIdLock = null;
                }
            }

            newGameSession.Revision = 0;

            var sessionState = gameData.GetSessionState(newGameSession.Id);
            sessionState.SyncTime = syncTime;
            sessionState.ClientVersion = clientVersion;

            await SendPermissionDataAsync(newGameSession, user);

            SendVillageInfo(newGameSession);

            return GenerateSessionToken(token, user, newGameSession);
        }

        public bool AttachPlayersToSession(SessionToken session, Guid[] characterIds)
        {
            var s = gameData.GetSession(session.SessionId);
            if (s != null)
            {
                var chars = gameData.GetSessionCharacters(s);
                foreach (var c in chars)
                {
                    c.UserIdLock = null;
                }
            }

            var result = false;
            foreach (var val in characterIds)
            {
                result = playerManager.AddPlayer(session, val) != null || result;
            }
            return result;
        }

        public void SendVillageInfo(DataModels.GameSession newGameSession)
        {
            var village = gameData.GetOrCreateVillageBySession(newGameSession);
            var villageHouses = gameData.GetOrCreateVillageHouses(village);
            var villageInfoEvent = gameData.CreateSessionEvent(
                GameEventType.VillageInfo,
                newGameSession,
                new VillageInfo
                {
                    Name = village.Name,
                    Level = village.Level,
                    Experience = village.Experience,
                    Houses = villageHouses.Select(x =>
                       new VillageHouseInfo
                       {
                           Owner = x.UserId != null
                               ? gameData.GetUser(x.UserId.Value).UserId
                               : null,
                           Slot = x.Slot,
                           Type = x.Type
                       }
                    ).ToList()
                }
            );

            gameData.Add(villageInfoEvent);
        }

        public async Task SendPermissionDataAsync(DataModels.GameSession gameSession, DataModels.User user = null)
        {
            user = user ?? gameData.GetUser(gameSession.UserId);
            var isAdmin = user.IsAdmin.GetValueOrDefault();
            var isModerator = user.IsModerator.GetValueOrDefault();
            var subInfo = await twitchClient.GetSubscriberAsync(user.UserId);
            var subscriptionTier = 0;
            var patreonTier = user.PatreonTier ?? 0;

            var expMultiplierLimit = 0;
            if (subInfo != null && int.TryParse(subInfo.Tier, out subscriptionTier))
            {
                subscriptionTier /= 1000;
                expMultiplierLimit = MaxMultiplier[subscriptionTier];
                //subscriptionTier == 1 ? 10 : (subscriptionTier - 1) * 25;
            }

            if (patreonTier > 0)
            {
                var expMulti = MaxMultiplier[patreonTier];
                //patreonTier == 1 ? 10 : (patreonTier - 1) * 25;
                if (expMulti > expMultiplierLimit)
                {
                    expMultiplierLimit = expMulti;
                    subscriptionTier = patreonTier;
                }
            }

            if (isModerator)
            {
                subscriptionTier = 3;
                expMultiplierLimit = MaxMultiplier[subscriptionTier];
            }

            if (isAdmin)
            {
                subscriptionTier = 3;
                expMultiplierLimit = 50000000;
            }

            if (isAdmin || isModerator || subInfo != null || patreonTier > 0)
            {
                var permissionEvent = gameData.CreateSessionEvent(
                    GameEventType.PermissionChange,
                    gameSession,
                    new Permissions
                    {
                        IsAdministrator = user.IsAdmin ?? false,
                        IsModerator = user.IsModerator ?? false,
                        SubscriberTier = subscriptionTier,
                        ExpMultiplierLimit = expMultiplierLimit,
                    });

                gameData.Add(permissionEvent);
            }
        }

        public bool EndSessionAndRaid(
            SessionToken token, string userIdOrUsername, bool isWarRaid)
        {
            var currentSession = gameData.GetSession(token.SessionId);
            if (currentSession == null)
                return false;

            var sessionUser = gameData.GetUser(currentSession.UserId);
            var user = gameData.FindUser(userIdOrUsername);
            if (user == null)
                return false;

            var targetSession = gameData.GetUserSession(user.Id);
            if (targetSession == null)
                return false;

            var characters = gameData.GetSessionCharacters(currentSession);

            var ge = gameData.CreateSessionEvent(
                isWarRaid ? GameEventType.WarRaid : GameEventType.Raid,
                targetSession, new
                {
                    RaiderUserName = sessionUser.UserName,
                    RaiderUserId = sessionUser.UserId,
                    Players = characters.Select(x => gameData.GetUser(x.UserId).UserId).ToArray()
                });

            gameData.Add(ge);
            EndSession(token);
            return true;
        }

        public void EndSession(SessionToken token)
        {
            var session = gameData.GetSession(token.SessionId);
            if (session == null)
            {
                return;
            }

            EndSession(session);
        }

        public void EndSession(DataModels.GameSession session)
        {
            var characters = gameData.GetSessionCharacters(session);

            foreach (var character in characters)
            {
                character.UserIdLock = null;
            }

            session.Status = (int)SessionStatus.Inactive;
            session.Stopped = DateTime.UtcNow;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SessionToken Get(string sessionToken)
        {
            var json = Base64Decode(sessionToken);
            return JSON.Parse<SessionToken>(json);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string Base64Decode(string str)
        {
            var data = System.Convert.FromBase64String(str);
            return Encoding.UTF8.GetString(data);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static SessionToken GenerateSessionToken(
            AuthToken token,
            DataModels.User user,
            DataModels.GameSession session)
        {
            return new SessionToken
            {
                AuthToken = token.Token,
                ExpiresUtc = DateTime.UtcNow + TimeSpan.FromDays(180),
                SessionId = session.Id,
                StartedUtc = session.Started,
                TwitchDisplayName = user.DisplayName,
                TwitchUserId = user.UserId,
                TwitchUserName = user.UserName
            };
        }
    }
}

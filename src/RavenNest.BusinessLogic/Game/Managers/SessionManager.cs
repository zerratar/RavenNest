using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Net;
using RavenNest.DataModels;
using RavenNest.Models;

namespace RavenNest.BusinessLogic.Game
{
    public class SessionManager : ISessionManager
    {
        private readonly ITwitchClient twitchClient;
        private readonly IGameData gameData;

        public SessionManager(ITwitchClient twitchClient, IGameData gameData)
        {
            this.twitchClient = twitchClient;
            this.gameData = gameData;
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

            var userId = token.UserId;
            var activeSession = gameData.GetUserSession(userId);
            // x => x.UserId == userId && x.Status == (int)SessionStatus.Active

            if (activeSession != null)
            {
                EndSession(activeSession);
            }

            var newGameSession = gameData.CreateSession(userId);

            gameData.Add(newGameSession);

            var sessionState = gameData.GetSessionState(newGameSession.Id);
            sessionState.SyncTime = syncTime;

            var isAdmin = user.IsAdmin.GetValueOrDefault();
            var isModerator = user.IsModerator.GetValueOrDefault();
            var subInfo = await twitchClient.GetSubscriberAsync(user.UserId);
            var subscriptionTier = 0;
            var expMultiplierLimit = 0;
            if (subInfo != null && int.TryParse(subInfo.Tier, out subscriptionTier))
            {
                subscriptionTier /= 1000;
                expMultiplierLimit = subscriptionTier == 1 ? 10 : (subscriptionTier - 1) * 25;
            }
            if (isModerator)
            {
                subscriptionTier = 3;
                expMultiplierLimit = 50;
            }
            if (isAdmin)
            {
                subscriptionTier = 3;
                expMultiplierLimit = 5000;
            }
            if (isAdmin || isModerator || subInfo != null)
            {
                var permissionEvent = gameData.CreateSessionEvent(
                    GameEventType.PermissionChange,
                    newGameSession,
                    new Permissions
                    {
                        IsAdministrator = user.IsAdmin ?? false,
                        IsModerator = user.IsModerator ?? false,
                        SubscriberTier = subscriptionTier,
                        ExpMultiplierLimit = expMultiplierLimit,
                    });

                gameData.Add(permissionEvent);
            }

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

            return GenerateSessionToken(token, newGameSession);
        }

        public bool EndSessionAndRaid(
            SessionToken token, string userIdOrUsername, bool isWarRaid)
        {
            var currentSession = gameData.GetSession(token.SessionId);
            if (currentSession == null)
            {
                return false;
            }

            var sessionUser = gameData.GetUser(currentSession.UserId);
            var user = gameData.FindUser(userIdOrUsername);
            if (user == null)
            {
                //await EndSessionAsync(token);
                return false;
            }

            var targetSession = gameData.GetUserSession(user.Id);
            if (targetSession == null)
            {
                //await EndSessionAsync(token);
                return false;
            }


            //var lastEvent = gameData.GetNextEventRevision(x => x.GameSessionId == targetSession.Id);
            //if (lastEvent != null) revision = lastEvent.Revision + 1;


            //var characters = await db.Character
            //    .Include(x => x.User)
            //    .Where(x => x.UserIdLock == currentSession.UserId && x.LastUsed != null && x.LastUsed >= currentSession.Started)
            //    .OrderByDescending(x => x.LastUsed)
            //    .ToListAsync();

            var characters = gameData.GetSessionCharacters(currentSession);

            //  var revision = gameData.GetNextGameEventRevision(targetSession.Id);

            var ge = gameData.CreateSessionEvent(
                isWarRaid ? GameEventType.WarRaid : GameEventType.Raid,
                targetSession, new
                {
                    RaiderUserName = sessionUser.UserName,
                    RaiderUserId = sessionUser.UserId,
                    Players = characters.Select(x => gameData.GetUser(x.UserId).UserId).ToArray()
                });

            //var ge = new DataModels.GameEvent
            //{
            //    Id = Guid.NewGuid(),
            //    GameSessionId = targetSession.Id,
            //    GameSession = targetSession,
            //    Revision = revision,
            //    Type = isWarRaid
            //        ? (int)GameEventType.WarRaid
            //        : (int)GameEventType.Raid,
            //    Data = JsonConvert.SerializeObject(new
            //    {
            //        RaiderUserName = currentSession.User.UserName,
            //        RaiderUserId = currentSession.User.UserId,
            //        Players = characters.Select(x => x.User.UserId).ToArray()
            //    })
            //};

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
        private static SessionToken GenerateSessionToken(AuthToken token, DataModels.GameSession session)
        {
            return new SessionToken
            {
                AuthToken = token.Token,
                ExpiresUtc = DateTime.UtcNow + TimeSpan.FromDays(180),
                SessionId = session.Id,
                StartedUtc = session.Started
            };
        }
    }
}
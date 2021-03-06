﻿using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Net;
using RavenNest.Models;

namespace RavenNest.BusinessLogic.Game
{
    public class SessionManager : ISessionManager
    {
        private readonly ILogger<SessionManager> logger;
        private readonly ITwitchClient twitchClient;
        private readonly IGameData gameData;
        private readonly IPlayerManager playerManager;

        private readonly int[] MaxMultiplier = new int[]
        {
            //0, 10, 15, 20
            //0, 15, 30, 50
            999, 999, 999, 999, 999, 999, 999, 999, 999, 999, 999
        };

        public SessionManager(
            ILogger<SessionManager> logger,
            ITwitchClient twitchClient,
            IGameData gameData,
            IPlayerManager playerManager)
        {
            this.logger = logger;
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
                //logger.LogError("Unable to start session for user: " + token.UserId + $", client version: {clientVersion}, accessKey: {accessKey}");
                return null;
            }

            if (clientVersion.ToLower() != game.ClientVersion.ToLower())
            {
                var evnum = game.ClientVersion.Replace("a", "");
                var vnum = clientVersion.Replace("a", "");
                if (!Version.TryParse(vnum, out var cv) || !Version.TryParse(evnum, out var ev) || cv <= ev)
                {
                    //logger.LogError("Unable to start session for user: " + token.UserId + $", client version: {clientVersion}, accessKey: {accessKey}");
                    return null; // new SessionToken();
                }
            }

            var userId = token.UserId;
            var activeSession = gameData.GetUserSession(userId);
            // x => x.UserId == userId && x.Status == (int)SessionStatus.Active

            if (activeSession != null &&
                DateTime.UtcNow - activeSession.Updated.GetValueOrDefault() >= TimeSpan.FromMinutes(30))
            {
                activeSession.Status = (int)SessionStatus.Inactive;
                activeSession.Stopped = DateTime.UtcNow;
                activeSession = null;
            }

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

            SendPermissionData(newGameSession, user);

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

        public void SendExpMultiplier(DataModels.GameSession session)
        {
            var activeEvent = gameData.GetActiveExpMultiplierEvent();
            if (activeEvent == null)
                return;

            var userId = session.UserId;
            var user = gameData.GetUser(userId);
            var patreonTier = user.PatreonTier.GetValueOrDefault();

            var multiplier = MaxMultiplier[patreonTier];
            if (multiplier > 0)
            {
                var expMulti = Math.Min(multiplier, activeEvent.Multiplier);
                var expEvent = gameData.CreateSessionEvent(
                      GameEventType.ExpMultiplier,
                      session,
                      new ExpMultiplier
                      {
                          EndTime = activeEvent.EndTime,
                          EventName = activeEvent.EventName,
                          Multiplier = expMulti,
                          StartTime = activeEvent.StartTime
                      }
                  );
                gameData.Add(expEvent);
            }
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

        public void SendPermissionData(DataModels.GameSession gameSession, DataModels.User user = null)
        {
            if (gameData == null)
            {
                logger.LogError(nameof(gameData) + " is null. unable to send permission data.");
                return;
            }

            if (gameSession == null)
            {
                logger.LogError(nameof(gameSession) + " is null. Unable to send permission data.");
                return;
            }

            user = user ?? gameData.GetUser(gameSession.UserId);
            var isAdmin = user.IsAdmin.GetValueOrDefault();
            var isModerator = user.IsModerator.GetValueOrDefault();
            //var subInfo = await twitchClient.GetSubscriberAsync(user.UserId);
            var patreonTier = user.PatreonTier ?? 0;

            var subscriptionTier = patreonTier;
            var expMultiplierLimit = MaxMultiplier[patreonTier];

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

        public bool EndSessionAndRaid(
            SessionToken token, string userIdOrUsername, bool isWarRaid)
        {
            var currentSession = gameData.GetSession(token.SessionId);
            if (currentSession == null)
            {
                logger.LogError($"Unable to do a streamer raid. No active session for {token.TwitchUserName}.");
                return false;
            }

            var sessionUser = gameData.GetUser(currentSession.UserId);
            if (userIdOrUsername.StartsWith("war ", StringComparison.OrdinalIgnoreCase))
            {
                userIdOrUsername = userIdOrUsername.Replace("war ", "", StringComparison.OrdinalIgnoreCase);
                isWarRaid = true;
            }

            var user = gameData.FindUser(userIdOrUsername);
            if (user == null)
            {
                logger.LogError($"Unable to do a streamer raid. No user found for {userIdOrUsername}.");
                return false;
            }

            var targetSession = gameData.GetUserSession(user.Id);
            if (targetSession == null)
            {
                logger.LogError($"Unable to do a streamer raid. Target user {userIdOrUsername} does not have an active session.");
                return false;
            }

            var characters = gameData.GetSessionCharacters(currentSession);

            //var state = gameData.GetSessionState(token.SessionId);
            var ge = gameData.CreateSessionEvent(
                isWarRaid ? GameEventType.WarRaid : GameEventType.Raid,

                targetSession, new StreamRaidInfo
                {
                    RaiderUserName = sessionUser.UserName,
                    RaiderUserId = sessionUser.UserId,
                    Players = characters.Select(x =>
                    {
                        var u = gameData.GetUser(x.UserId);
                        return new UserCharacter { CharacterId = x.Id, UserId = u?.UserId, Username = u?.UserName };
                    }).ToList()
                    //clientVersion != null && clientVersion >= characterIdClientVersion
                    //? characters.Select(x => x.Id.ToString()).ToArray()
                    //: characters.Select(x => gameData.GetUser(x.UserId).UserId).ToArray()
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

        public void SendServerTime(DataModels.GameSession session)
        {
            var serverTime = gameData.CreateSessionEvent(
                GameEventType.ServerTime,
                session,
                new ServerTime
                {
                    TimeUtc = DateTime.UtcNow
                }
            );
            gameData.Add(serverTime);
        }

        public void UpdateSessionState(SessionToken sessionToken, ClientSyncUpdate update)
        {
            var session = gameData.GetSession(sessionToken.SessionId);
            if (session == null) return;
            var owner = gameData.GetUser(session.UserId);
            if (owner == null) return;

            var sessionState = gameData.GetSessionState(session.Id);
            if (sessionState != null)
            {
                sessionState.ClientVersion = update.ClientVersion;
            }
        }

        public void RecordTimeMismatch(SessionToken sessionToken, TimeSyncUpdate update)
        {
            //var session = gameData.GetSession(sessionToken.SessionId);
            //if (session == null) return;
            //var owner = gameData.GetUser(session.UserId);
            //if (owner == null) return;

            //logger.LogError("Session by " + owner.UserName + " has a time mismatch of " + update.Delta.TotalSeconds + " seconds. Server Time: " + update.ServerTime + ", Local Time: " + update.LocalTime);
            //if (update.Delta.TotalSeconds >= 3600)
            //{
            //    logger.LogError("Session by " + owner.UserName + " Terminated due to high time mismatch. Potential speedhack");
            //    EndSession(sessionToken);
            //}
        }
    }
}

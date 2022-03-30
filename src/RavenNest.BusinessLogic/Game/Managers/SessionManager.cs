using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Net;
using RavenNest.BusinessLogic.Twitch.Extension;
using RavenNest.Models;
using RavenNest.Sessions;

namespace RavenNest.BusinessLogic.Game
{
    public class SessionManager : ISessionManager
    {
        private readonly ILogger<SessionManager> logger;
        private readonly ITwitchClient twitchClient;
        private readonly IGameData gameData;
        private readonly IPlayerManager playerManager;
        private readonly IVillageManager villageManager;
        private readonly IExtensionWebSocketConnectionProvider extWsConnectionProvider;
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
            IPlayerManager playerManager,
            IVillageManager villageManager,
            IExtensionWebSocketConnectionProvider extWsConnectionProvider)
        {
            this.logger = logger;
            this.twitchClient = twitchClient;
            this.gameData = gameData;
            this.playerManager = playerManager;
            this.villageManager = villageManager;
            this.extWsConnectionProvider = extWsConnectionProvider;
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
                var expectedVersionStr = game.ClientVersion.Replace("a", "");
                var clientVersionStr = clientVersion.Replace("a", "");
                if (!Version.TryParse(clientVersionStr, out var version) || !Version.TryParse(expectedVersionStr, out var expectedVersion) || version < expectedVersion)
                {
                    //logger.LogError("Unable to start session for user: " + token.UserId + $", client version: {clientVersion}, accessKey: {accessKey}");
                    return null; // new SessionToken();
                }
            }

            var userId = token.UserId;

            gameData.ClearAllCharacterSessionStates(userId);

            var activeSession = gameData.GetSessionByUserId(userId);
            // x => x.UserId == userId && x.Status == (int)SessionStatus.Active
            var oldSessionExpired = false;
            var oldSession = activeSession;
            if (activeSession != null)
            {
                oldSessionExpired = DateTime.UtcNow - activeSession.Updated.GetValueOrDefault() >= TimeSpan.FromMinutes(30);
                if (oldSessionExpired)
                {
                    activeSession.Status = (int)SessionStatus.Inactive;
                    activeSession.Stopped = DateTime.UtcNow;
                    activeSession = null;
                }
            }

            var newGameSession = activeSession ?? gameData.CreateSession(userId);
            if (activeSession == null)
            {
                gameData.Add(newGameSession);
            }

            if (oldSession != null && !oldSessionExpired)
            {
                logger.LogError("BeginSessionAsync was called while an existing session is active. User: " + user.UserName + ". Previous players will not be cleared.");
            }
            else
            {
                var activeChars = gameData.GetSessionCharacters(newGameSession);
                if (activeChars != null)
                {
                    foreach (var c in activeChars)
                    {
                        c.UserIdLock = null;
                    }
                }
                //#if DEBUG
                //                logger.LogDebug(user.UserName + " game session started. " + activeChars.Count + " characters cleared.");
                //#endif
            }

            newGameSession.Revision = 0;

            var sessionState = gameData.GetSessionState(newGameSession.Id);
            sessionState.SyncTime = syncTime;
            sessionState.ClientVersion = clientVersion;

            SendPermissionData(newGameSession, user);
            SendVillageInfo(newGameSession);

            return GenerateSessionToken(token, user, newGameSession, clientVersion);
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
            foreach (var id in characterIds)
            {
                var c = gameData.GetCharacter(id);
                //if (c == null || (c.UserIdLock != null && c.UserIdLock != session))
                result = playerManager.AddPlayer(session, id) != null || result;
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
            //if (multiplier >= 0)
            //{
            var expMulti = Math.Min(multiplier, activeEvent.Multiplier);
            var expEvent = gameData.CreateSessionEvent(GameEventType.ExpMultiplier,
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
            //}
        }

        public void SendVillageInfo(DataModels.GameSession newGameSession)
        {
            var villageInfo = villageManager.GetVillageInfo(newGameSession.Id);
            var villageInfoEvent = gameData.CreateSessionEvent(GameEventType.VillageInfo,
                newGameSession,
                villageInfo
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

            var permissionEvent = gameData.CreateSessionEvent(GameEventType.PermissionChange,
                gameSession,
                new Permissions
                {
                    IsAdministrator = user.IsAdmin ?? false,
                    IsModerator = user.IsModerator ?? false,
                    SubscriberTier = subscriptionTier,
                    ExpMultiplierLimit = expMultiplierLimit,
                    StrictLevelRequirements = true,
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

            var targetSession = gameData.GetSessionByUserId(user.Id);
            if (targetSession == null)
            {
                logger.LogError($"Unable to do a streamer raid. Target user {userIdOrUsername} does not have an active session.");
                return false;
            }

            var characters = gameData.GetSessionCharacters(currentSession);

            //var state = gameData.GetSessionState(token.SessionId);
            var ge = gameData.CreateSessionEvent(isWarRaid ? GameEventType.WarRaid : GameEventType.Raid,
                targetSession, new StreamRaidInfo
                {
                    RaiderUserName = sessionUser.UserName,
                    RaiderUserId = sessionUser.UserId,
                    Players = characters.Select(x =>
                    {
                        var u = gameData.GetUser(x.UserId);
                        return new UserCharacter { CharacterId = x.Id, UserId = u?.UserId, Username = u?.UserName };
                    }).ToList()
                });

            gameData.Add(ge);
            EndSession(token);

#if DEBUG
            logger.LogDebug(sessionUser + " is " + (isWarRaid ? "initiating a raid war on" : "raiding") + " " + user.DisplayName + " with " + characters.Count + " players.");
#endif

            return true;
        }

        public void EndSession(SessionToken token)
        {
            var session = gameData.GetSession(token.SessionId);
            if (session == null)
            {
#if DEBUG
                logger.LogDebug("Unable to end session " + token.TwitchUserName + ", no such session exists.");
#endif
                return;
            }

            EndSession(session);
        }

        public void EndSession(DataModels.GameSession session)
        {
            var characters = gameData.GetSessionCharacters(session);
            var owner = gameData.GetUser(session.UserId);

            var data = new StreamerInfo();
            data.StreamerUserId = owner.UserId;
            data.StreamerUserName = owner.UserName;
            data.IsRavenfallRunning = false;
            data.StreamerSessionId = null;
            data.Started = null;

            var state = gameData.GetSessionState(session.Id);
            if (state != null)
            {
                data.ClientVersion = state.ClientVersion;
            }

            if (extWsConnectionProvider.TryGetAllByStreamer(session.UserId, out var allConnections))
            {
                foreach (var connection in allConnections)
                {
                    connection.SendAsync(data);
                }
            }

            foreach (var character in characters)
            {
                character.UserIdLock = null;
            }

            session.Status = (int)SessionStatus.Inactive;
            session.Stopped = DateTime.UtcNow;

            gameData.ClearAllCharacterSessionStates(session.UserId);
            //gameData.ClearCharacterSessionStates(session.Id);

#if DEBUG
            logger.LogDebug(owner.UserName + " game session ended. " + characters.Count + " characters cleared.");
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SessionToken Get(string sessionToken)
        {
            var json = Base64Decode(sessionToken);
            var token = JSON.Parse<SessionToken>(json);

            if (token != null && !string.IsNullOrEmpty(token.ClientVersion))
            {
                var state = gameData.GetSessionState(token.SessionId);
                if (state != null && string.IsNullOrEmpty(state.ClientVersion))
                {
                    state.ClientVersion = token.ClientVersion;
                }
            }
            return token;
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
            DataModels.GameSession session,
            string clientVersion)
        {
            return new SessionToken
            {
                AuthToken = token.Token,
                ExpiresUtc = DateTime.UtcNow + TimeSpan.FromDays(180),
                SessionId = session.Id,
                StartedUtc = session.Started,
                TwitchDisplayName = user.DisplayName,
                TwitchUserId = user.UserId,
                TwitchUserName = user.UserName,
                ClientVersion = clientVersion
            };
        }

        public void SendPubSubToken(DataModels.GameSession session, string pubsubAccessToken)
        {
            var serverTime = gameData.CreateSessionEvent(GameEventType.PubSubToken,
                session,
                new PubSubToken
                {
                    Token = pubsubAccessToken
                }
            );
            gameData.Add(serverTime);
        }
        public void SendServerTime(DataModels.GameSession session)
        {
            var serverTime = gameData.CreateSessionEvent(GameEventType.ServerTime,
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
            var session = gameData.GetSession(sessionToken.SessionId);
            if (session == null) return;
            var owner = gameData.GetUser(session.UserId);
            if (owner == null) return;

            logger.LogError("Session by " + owner.UserName + " has a time mismatch of " + update.Delta.TotalSeconds + " seconds. Server Time: " + update.ServerTime + ", Local Time: " + update.LocalTime);
            //if (update.Delta.TotalSeconds >= 3600)
            //{
            //    logger.LogError("Session by " + owner.UserName + " Terminated due to high time mismatch. Potential speedhack");
            //    EndSession(sessionToken);
            //}
        }

    }
}

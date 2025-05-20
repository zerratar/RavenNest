using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Net;
using RavenNest.BusinessLogic.Net.DeltaTcpLib;
using RavenNest.BusinessLogic.ScriptParser;
using RavenNest.BusinessLogic.Twitch.Extension;
using RavenNest.DataModels;
using RavenNest.Models;
using RavenNest.Sessions;
using static RavenNest.Models.Tv.Episode;
using static RavenNest.Twitch.TwitchRequests;

namespace RavenNest.BusinessLogic.Game
{
    public class SessionManager : ISessionTokenProvider
    {
        private readonly ILogger<SessionManager> logger;
        private readonly ITwitchClient twitchClient;
        private readonly GameData gameData;
        private readonly PlayerManager playerManager;
        private readonly VillageManager villageManager;
        private readonly ITwitchExtensionConnectionProvider extWsConnectionProvider;
        private readonly ITcpSocketApiConnectionProvider tcpConnectionProvider;

        public const int MaxPlayerExpMultiplier = 100;
        public const double RaidExpFactor = 1.0;
        public const double DungeonExpFactor = 1.0;

        public const int ExpMultiplierStartTimeMinutes = 15;
        public const int ExpMultiplierLastTimeMinutes = 50;
        public const int ExpMultiplierMinutesPerScroll = 5;

        private readonly int[] MaxSystemExpMultiplier = new int[]
        {
            //0, 10, 15, 20
            //0, 15, 30, 50
            999, 999, 999, 999, 999, 999, 999, 999, 999, 999, 999
        };

        public SessionManager(
            ILogger<SessionManager> logger,
            ITwitchClient twitchClient,
            GameData gameData,
            PlayerManager playerManager,
            VillageManager villageManager,
            ITwitchExtensionConnectionProvider extWsConnectionProvider,
            ITcpSocketApiConnectionProvider tcpConnectionProvider)
        {
            this.logger = logger;
            this.twitchClient = twitchClient;
            this.gameData = gameData;
            this.playerManager = playerManager;
            this.villageManager = villageManager;
            this.extWsConnectionProvider = extWsConnectionProvider;
            this.tcpConnectionProvider = tcpConnectionProvider;
        }

        public bool IsExpectedVersion(string clientVersion, bool skipVersion = false)
        {
            var game = gameData.Client;

            if (!string.Equals(clientVersion, game.ClientVersion, StringComparison.OrdinalIgnoreCase))
            {
                // this shouldnt happen, but you never know.
                if (!GameVersion.TryParse(clientVersion, out var version))
                {
                    return false;
                }

                if (!GameVersion.TryParse(game.ClientVersion, out var expectedVersion))
                {
                    return false;
                }

                // if skipUpdate is true, user is attempting to create a new session without enforcing a new version.
                // this should be allowed as long as the version is not breaking changes.

                if (skipVersion)
                {
                    return true;
                }

                return version >= expectedVersion;
            }

            return true;
        }

        public Task<BeginSessionResult> BeginSessionAsync(AuthToken token, string clientVersion, string accessKey, float gameTime, bool skipUpdate = false)
        {
            // if skipUpdate is true, user is attempting to create a new session without enforcing a new version.
            // this should be allowed as long as the version is not breaking changes.

            var game = gameData.Client;
            var user = gameData.GetUser(token.UserId);
            if (user == null)
            {
                return Task.FromResult(BeginSessionResult.UserDoesNotExist);
            }

            if (game.AccessKey != accessKey || !IsExpectedVersion(clientVersion, skipUpdate))
            {
                var invalidAccessKey = BeginSessionResult.InvalidVersion;
                invalidAccessKey.ExpectedClientVersion = game.ClientVersion;
                return Task.FromResult(invalidAccessKey);
            }

            var userId = token.UserId;

            gameData.ClearAllCharacterSessionStates(userId);

            var activeSession = gameData.GetSessionByUserId(userId);
            var now = DateTime.UtcNow;
            var oldSessionExpired = false;
            var oldSession = activeSession;
            if (activeSession != null)
            {
                oldSessionExpired = now - activeSession.Updated.GetValueOrDefault() >= TimeSpan.FromMinutes(30);
                if (oldSessionExpired)
                {
                    activeSession.Status = (int)SessionStatus.Inactive;
                    activeSession.Stopped = now;
                    activeSession = null;
                }
            }

            var newGameSession = activeSession ?? GameData.CreateSession(userId);
            if (activeSession == null)
            {
                newGameSession.Revision = 0;
                gameData.Add(newGameSession);
                activeSession = newGameSession;
            }


            activeSession.Refreshed = now;

            //if (oldSession != null && !oldSessionExpired)
            //{
            //    logger.LogError("BeginSessionAsync was called while an existing session is active. User: " + user.UserName + ". Previous players will not be cleared.");
            //}
            //else
            //{
            //    ClearUserLocks(newGameSession);
            //}

            var sessionState = gameData.GetSessionState(newGameSession.Id);
            sessionState.SyncTime = gameTime;
            sessionState.ClientVersion = clientVersion;

            var sessionToken = GenerateSessionToken(token, user, newGameSession, clientVersion);

            var userSettings = gameData.GetUserSettings(userId);
            if (userSettings != null)
            {
                userSettings["client_version"] = clientVersion;
            }

            return Task.FromResult(new BeginSessionResult
            {
                ExpectedClientVersion = game.ClientVersion,
                SessionToken = sessionToken,
                State = BeginSessionResultState.Success,
                ExpMultiplier = GetExpMultiplier(),
                Permissions = GetSessionSettings(user),
                Village = villageManager.GetVillageInfo(newGameSession),
                UserSettings = userSettings,
            });
        }

        public ExpMultiplier GetExpMultiplier()
        {
            var activeEvent = gameData.GetActiveExpMultiplierEvent();
            if (activeEvent == null)
                return new ExpMultiplier();

            return new ExpMultiplier
            {
                EndTime = activeEvent.EndTime,
                EventName = activeEvent.EventName,
                StartedByPlayer = activeEvent.StartedByPlayer,
                Multiplier = activeEvent.Multiplier,
                StartTime = activeEvent.StartTime
            };
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
                var expectedVersionStr = game.ClientVersion;
                var clientVersionStr = clientVersion;

                if (!GameVersion.TryParse(clientVersionStr, out var version) || !GameVersion.TryParse(expectedVersionStr, out var expectedVersion) || version < expectedVersion)
                {
                    //logger.LogError("Unable to start session for user: " + token.UserId + $", client version: {clientVersion}, accessKey: {accessKey}");
                    return null; // new SessionToken();
                }
            }

            var userId = token.UserId;
            var now = DateTime.UtcNow;

            gameData.ClearAllCharacterSessionStates(userId);

            var activeSession = gameData.GetSessionByUserId(userId);
            // x => x.UserId == userId && x.Status == (int)SessionStatus.Active
            var oldSessionExpired = false;
            var oldSession = activeSession;
            if (activeSession != null)
            {
                oldSessionExpired = now - activeSession.Updated.GetValueOrDefault() >= TimeSpan.FromMinutes(30);
                if (oldSessionExpired)
                {
                    activeSession.Status = (int)SessionStatus.Inactive;
                    activeSession.Stopped = now;
                    activeSession = null;
                }
            }

            var newGameSession = activeSession ?? GameData.CreateSession(userId);
            if (activeSession == null)
            {
                newGameSession.Revision = 0;
                gameData.Add(newGameSession);
                activeSession = newGameSession;
            }

            activeSession.Refreshed = now;

            var sessionState = gameData.GetSessionState(newGameSession.Id);
            sessionState.SyncTime = syncTime;
            sessionState.ClientVersion = clientVersion;

            SendSessionSettings(newGameSession, user);
            SendVillageInfo(newGameSession);

            return GenerateSessionToken(token, user, newGameSession, clientVersion);
        }

        public bool AttachPlayersToSession(SessionToken session, Guid[] characterIds)
        {
            var s = gameData.GetSession(session.SessionId);
            if (s != null)
            {
                ClearUserLocks(s);
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
            DataModels.GameEvent expEvent = CreateExpMultiplierEvent(session);
            gameData.EnqueueGameEvent(expEvent);
        }

        private void ClearUserLocks(DataModels.GameSession s)
        {
            var characters = gameData.GetSessionCharacters(s);
            foreach (var c in characters)
            {
                if (c.UserIdLock != null)
                    c.PrevUserIdLock = c.UserIdLock;
                c.UserIdLock = null;
            }
        }
        private DataModels.GameEvent CreateExpMultiplierEvent(DataModels.GameSession session)
        {
            var activeEvent = gameData.GetActiveExpMultiplierEvent();
            if (activeEvent == null)
                return null;

            var userId = session.UserId;
            var user = gameData.GetUser(userId);
            var patreonTier = user.PatreonTier.GetValueOrDefault();
            var multiplier = MaxSystemExpMultiplier[patreonTier];
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
            return expEvent;
        }

        public void SendVillageInfo(DataModels.GameSession newGameSession)
        {
            DataModels.GameEvent villageInfoEvent = CreateVillageInfoEvent(newGameSession);
            gameData.EnqueueGameEvent(villageInfoEvent);
        }

        public void SendSessionSettings(DataModels.GameSession gameSession, DataModels.User user = null)
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
            if (user == null)
            {
                logger.LogError(nameof(user) + " is null. Unable to send permission data.");
                return;
            }
            DataModels.GameEvent permissionEvent = CreateSessionSettingsChangeEvent(gameSession, user);

            gameData.EnqueueGameEvent(permissionEvent);
        }

        private DataModels.GameEvent CreateVillageInfoEvent(DataModels.GameSession newGameSession)
        {
            var villageInfo = villageManager.GetVillageInfo(newGameSession.Id);
            var villageInfoEvent = gameData.CreateSessionEvent(GameEventType.VillageInfo,
                newGameSession,
                villageInfo
            );
            return villageInfoEvent;
        }

        private DataModels.GameEvent CreateSessionSettingsChangeEvent(DataModels.GameSession gameSession, DataModels.User user)
        {
            if (user == null)
            {
                return null;
            }

            var data = GetSessionSettings(user);
            var permissionEvent = gameData.CreateSessionEvent(GameEventType.SessionSettingsChanged, gameSession, data);
            return permissionEvent;
        }

        private SessionSettings GetSessionSettings(DataModels.User user)
        {
            if (user == null)
            {
                return null;
            }

            var isAdmin = user.IsAdmin.GetValueOrDefault();
            var isModerator = user.IsModerator.GetValueOrDefault();
            //var subInfo = await twitchClient.GetSubscriberAsync(user.UserId);
            var patreonTier = user.PatreonTier ?? 0;

            var subscriptionTier = patreonTier;
            var expMultiplierLimit = MaxSystemExpMultiplier[patreonTier];

            if (isModerator)
            {
                subscriptionTier = 3;
                expMultiplierLimit = MaxSystemExpMultiplier[subscriptionTier];
            }

            if (isAdmin)
            {
                subscriptionTier = 3;
                expMultiplierLimit = 50000000;
            }

            var data = new SessionSettings
            {
                IsAdministrator = user.IsAdmin ?? false,
                IsModerator = user.IsModerator ?? false,
                SubscriberTier = subscriptionTier,
                ExpMultiplierLimit = expMultiplierLimit,
                PlayerExpMultiplierLimit = MaxPlayerExpMultiplier,
                StrictLevelRequirements = true,
                RaidExpFactor = RaidExpFactor,
                DungeonExpFactor = DungeonExpFactor,

                AutoRestCost = PlayerManager.AutoRestCostPerSecond,
                AutoJoinRaidCost = PlayerManager.AutoJoinRaidCost,
                AutoJoinDungeonCost = PlayerManager.AutoJoinDungeonCost,

                XP_EasyLevel = GameMath.Exp.EasyLevel,
                XP_EasyLevelIncrementDivider = GameMath.Exp.EasyLevelIncrementDivider,
                XP_GlobalMultiplierFactor = GameMath.Exp.GlobalMultiplierFactor,
                XP_IncrementMins = GameMath.Exp.IncrementMins,
            };
            return data;
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

            return EndSessionAndRaid(currentSession, targetSession, isWarRaid);
        }

        public bool EndSessionAndRaid(DataModels.GameSession raider, DataModels.GameSession target, bool isWarRaid)
        {
            var characters = gameData.GetActiveSessionCharacters(raider);
            var sessionUser = gameData.GetUser(raider.UserId);
            var user = gameData.GetUser(target.UserId);
            //var state = gameData.GetSessionState(token.SessionId);
            var ge = gameData.CreateSessionEvent(isWarRaid ? GameEventType.WarRaid : GameEventType.Raid,
                target, new StreamRaidInfo
                {
                    RaiderUserName = sessionUser.UserName,
                    RaiderUserId = sessionUser.Id,
                    Players = characters.Select(x =>
                    {
                        var u = gameData.GetUser(x.UserId);
                        return new StreamRaidPlayer
                        {
                            CharacterId = x.Id,
                            UserId = u?.Id ?? Guid.Empty,
                            Username = u?.UserName,
                        };
                    }).ToList()
                });

            gameData.EnqueueGameEvent(ge);
            EndSession(raider);

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
            var sessionOwner = gameData.GetUser(session.UserId);

            var data = new StreamerInfo();
            data.StreamerUserId = sessionOwner.UserId;
            data.StreamerUserName = sessionOwner.UserName;
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

            UpdateSessionPlayerList(sessionOwner.Id, characters);

            foreach (var character in characters)
            {
                if (character.UserIdLock != null)
                    character.PrevUserIdLock = character.UserIdLock;
                character.UserIdLock = null;
            }

            session.Status = (int)SessionStatus.Inactive;
            session.Stopped = DateTime.UtcNow;

            gameData.ClearAllCharacterSessionStates(session.UserId);
            //gameData.ClearCharacterSessionStates(session.Id);

#if DEBUG
            logger.LogDebug(sessionOwner.UserName + " game session ended. " + characters.Count + " characters cleared.");
#endif
        }

        public void UpdateSessionPlayerList(Guid userId)
        {
            var session = gameData.GetSessionByUserId(userId, false, false);
            var characters = gameData.GetSessionCharacters(session);
            UpdateSessionPlayerList(userId, characters);
        }

        public void UpdateSessionPlayerList(DataModels.GameSession session)
        {
            var characters = gameData.GetSessionCharacters(session);
            UpdateSessionPlayerList(session.UserId, characters);
        }

        public void UpdateSessionPlayerList(Guid ownerId, IReadOnlyList<DataModels.Character> sessionCharacters)
        {
            try
            {
                var sessionCharacterIds = sessionCharacters.Select(x => x.Id).ToList();
                var folder = System.IO.Path.Combine(FolderPaths.GeneratedDataPath, FolderPaths.SessionPlayers);
                if (!System.IO.Directory.Exists(folder))
                {
                    System.IO.Directory.CreateDirectory(folder);
                }

                var playerlistFile = System.IO.Path.Combine(folder, ownerId.ToString() + ".json");
                if (System.IO.File.Exists(playerlistFile))
                {
                    // if this file exists, try loading it so we can append the sessionCharacterIds
                    // but check if any of the existing has a userIdLock other than this user, null is fine since we should not have any characters with "null" in sessionCharacterIds. Therefor a player should never have been saved in the first place that was not part of this stream.
                    // If they have joined another stream their lock will be theirs.
                    // There is a slight chance of course that you may still take a player from the other stream if streamer A closes down game, player joins streamer B, streamer B closes down game and streamer A joins game
                    // Should we add a "previous user id lock" ? then we can compare that.
                    var existing = Newtonsoft.Json.JsonConvert.DeserializeObject<List<Guid>>(System.IO.File.ReadAllText(playerlistFile));
                    foreach (var e in existing)
                    {
                        var c = gameData.GetCharacter(e);
                        if (c == null || (c.UserIdLock != null && c.UserIdLock != ownerId))
                            continue;

                        if (!sessionCharacterIds.Contains(c.Id))
                        {
                            sessionCharacterIds.Add(c.Id);
                        }
                    }
                }

                // now save it!, this will be our character list that we know should be valid to generate a state file of.
                System.IO.File.WriteAllText(playerlistFile, Newtonsoft.Json.JsonConvert.SerializeObject(sessionCharacterIds));
            }
            catch { }
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

        internal SessionToken GetSessionTokenByCharacterId(Guid characterId, bool allowInactiveSessions = false)
        {
            var session = gameData.GetSessionByCharacterId(characterId, allowInactiveSessions);
            if (session == null) return null;
            var user = gameData.GetUser(session.UserId);

            var twitch = gameData.GetUserAccess(user.Id, "twitch");
            return new SessionToken
            {
                SessionId = session.Id,
                ExpiresUtc = DateTime.UtcNow + TimeSpan.FromDays(180),
                StartedUtc = session.Started,
                UserId = user.Id,
                DisplayName = user.DisplayName,
                UserName = user.UserName,
                TwitchDisplayName = twitch?.PlatformUsername,
                TwitchUserId = twitch?.PlatformId,
                TwitchUserName = twitch?.PlatformUsername,
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private SessionToken GenerateSessionToken(
            AuthToken token,
            DataModels.User user,
            DataModels.GameSession session,
            string clientVersion)
        {
            var twitch = gameData.GetUserAccess(user.Id, "twitch");
            return new SessionToken
            {
                AuthToken = token.Token,
                ExpiresUtc = DateTime.UtcNow + TimeSpan.FromDays(180),
                SessionId = session.Id,
                StartedUtc = session.Started,
                UserId = user.Id,
                DisplayName = user.DisplayName,
                UserName = user.UserName,
                TwitchDisplayName = twitch?.PlatformUsername,
                TwitchUserId = twitch?.PlatformId,
                TwitchUserName = twitch?.PlatformUsername,
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
            gameData.EnqueueGameEvent(serverTime);
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
            gameData.EnqueueGameEvent(serverTime);
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

        public StreamerInfo GetStreamerInfo(string broadcasterId, Guid userId)
        {
            var streamer = gameData.GetUserByTwitchId(broadcasterId);
            var result = new StreamerInfo();
            if (streamer != null)
            {
                result.StreamerUserId = broadcasterId;
                result.StreamerUserName = streamer.UserName;

                var gameSession = gameData.GetOwnedSessionByUserId(streamer.Id);

                result.IsRavenfallRunning = gameSession != null;
                result.StreamerSessionId = gameSession?.Id;

                if (gameSession != null)
                {
                    result.Started = gameSession.Started;

                    var state = gameData.GetSessionState(gameSession.Id);
                    if (state != null)
                    {
                        result.ClientVersion = state.ClientVersion;

                        if (!state.IsConnectedToClient)
                        {
                            result.IsRavenfallRunning = false;
                        }
                    }

                    var charactersInSession = gameData.GetActiveSessionCharacters(gameSession);
                    if (charactersInSession != null)
                    {
                        var u = gameData.GetUser(userId);
                        if (u != null)
                        {
                            var c = charactersInSession.FirstOrDefault(x => x.UserId == u.Id);
                            if (c != null)
                            {
                                result.JoinedCharacterId = c.Id;
                            }
                        }

                        result.PlayerCount = charactersInSession.Count;
                    }
                }
            }

            return result;
        }
    }
}

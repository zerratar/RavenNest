using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Game.Processors;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Threading;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Game;
using RavenNest.BusinessLogic.Twitch.Extension;
using System.Collections.Generic;
using RavenNest.Sessions;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;
using static RavenNest.BusinessLogic.Models.Patreon.API.PatreonIdentity;
using System.Diagnostics;
using RavenNest.DataModels;
using SessionToken = RavenNest.Models.SessionToken;

namespace RavenNest.BusinessLogic.Net
{
    public interface IGameProcessorManager : IDisposable
    {
        /// <summary>A connection of the streamer's client has authenticated: run the game loop for its session.</summary>
        void Start(SessionToken sessionToken, int connectionId);

        /// <summary>A connection has closed: end the game loop only when it was the streamer's last one.</summary>
        void Stop(SessionToken sessionToken, int connectionId);
    }

    public class GameProcessorManager : IGameProcessorManager
    {
        private readonly ConcurrentDictionary<Guid, GameInstance> gameInstances = new ConcurrentDictionary<Guid, GameInstance>();
        private readonly Dictionary<Guid, HashSet<int>> connections = new Dictionary<Guid, HashSet<int>>();
        private readonly object sync = new object();
        private readonly GameData gameData;
        private readonly SessionManager sessionManager;
        private readonly PlayerInventoryProvider inventoryProvider;
        private readonly IRavenBotApiClient ravenbotApi;
        private readonly ITwitchExtensionConnectionProvider extWsProvider;
        private readonly ITcpSocketApiConnectionProvider tcpConnectionProvider;
        private readonly ILogger<GameProcessorManager> logger;

        public GameProcessorManager(
            GameData gameData,
            SessionManager sessionManager,
            PlayerInventoryProvider inventoryProvider,
            IRavenBotApiClient ravenbotApi,
            ITwitchExtensionConnectionProvider extWsProvider,
            ITcpSocketApiConnectionProvider tcpConnectionProvider,
            ILogger<GameProcessorManager> logger)
        {
            this.gameData = gameData;
            this.sessionManager = sessionManager;
            this.inventoryProvider = inventoryProvider;
            this.ravenbotApi = ravenbotApi;
            this.extWsProvider = extWsProvider;
            this.tcpConnectionProvider = tcpConnectionProvider;
            this.logger = logger;
        }

        public void Dispose()
        {
            logger.LogDebug("Disposing all game instances...");
            foreach (var i in gameInstances)
            {
                if (i.Value != null)
                {
                    i.Value.Dispose();
                }
            }
        }

        public void Start(RavenNest.Models.SessionToken sessionToken, int connectionId)
        {
            var uid = sessionToken.UserId;
            GameInstance replaced = null;
            DataModels.SessionState state;

            // A streamer's client can have more than one connection for a moment, when it reconnects: the new one
            // is counted here and the old one's Stop does not end the game loop while another connection is up.
            // (Before, Start did nothing while an instance ran, and the old connection's Stop then disposed the only
            // instance, leaving the stream with no game loop and no events until the client restarted.)
            lock (sync)
            {
                if (!connections.TryGetValue(uid, out var open))
                {
                    connections[uid] = open = new HashSet<int>();
                }
                open.Add(connectionId);

                if (gameInstances.TryGetValue(uid, out var existing) && existing != null && existing.IsActive)
                {
                    if (existing.SessionToken.SessionId == sessionToken.SessionId)
                    {
                        return;
                    }

                    // a new session: the game was restarted, so the loop must follow the new session, not the old one
                    replaced = existing;
                }

                state = gameData.GetSessionState(sessionToken.SessionId);
                if (state != null)
                {
                    state.IsConnectedToClient = true;
                }

                var instance = new GameInstance(sessionToken, ravenbotApi, extWsProvider, tcpConnectionProvider, sessionManager, inventoryProvider, gameData, logger);
                gameInstances[uid] = instance;
                instance.Start();
            }

            if (replaced != null)
            {
                logger.LogInformation("[" + sessionToken.UserName + "] New session " + sessionToken.SessionId + " replaces the game loop of session " + replaced.SessionToken.SessionId);
                replaced.Dispose();
            }

            if (extWsProvider.TryGetAllByStreamer(sessionToken.UserId, out var allConnections))
            {
                var twitch = gameData.GetUserAccess(sessionToken.UserId, "twitch");
                var twitchUserId = twitch?.PlatformId ?? sessionToken.TwitchUserId;

                foreach (var c in allConnections)
                {

                    var data = sessionManager.GetStreamerInfo(twitchUserId, c.Session.UserId);
                    data.IsRavenfallRunning = true;
                    c.SendAsync(data);
                }
            }
        }

        public void Stop(SessionToken sessionToken, int connectionId)
        {
            try
            {
                if (sessionToken == null)
                {
                    return;
                }

                var uid = sessionToken.UserId;
                GameInstance instance;
                lock (sync)
                {
                    if (connections.TryGetValue(uid, out var open))
                    {
                        open.Remove(connectionId);
                        if (open.Count > 0)
                        {
                            // another connection of this streamer is still up (a reconnect): keep the game loop
                            return;
                        }
                        connections.Remove(uid);
                    }

                    if (!gameInstances.TryGetValue(uid, out instance))
                    {
                        return;
                    }

                    // only the loop of this session; a newer session's loop is not this connection's to end
                    if (instance != null && instance.SessionToken.SessionId != sessionToken.SessionId && instance.IsActive)
                    {
                        return;
                    }

                    gameInstances.Remove(uid, out _);
                }

                var state = gameData.GetSessionState(sessionToken.SessionId);
                if (state != null)
                {
                    state.IsConnectedToClient = false;
                }

                // do something
                if (instance != null)
                {
                    instance.Dispose();
                }

                if (extWsProvider.TryGetAllByStreamer(sessionToken.UserId, out var allConnections))
                {
                    var data = new StreamerInfo();
                    var twitch = gameData.GetUserAccess(sessionToken.UserId, "twitch");
                    data.StreamerUserId = twitch?.PlatformId ?? sessionToken.TwitchUserId;
                    data.StreamerUserName = twitch?.PlatformUsername ?? sessionToken.UserName;
                    data.IsRavenfallRunning = false;
                    data.StreamerSessionId = null;
                    data.Started = null;

                    if (state != null)
                    {
                        data.ClientVersion = state.ClientVersion;
                    }

                    foreach (var c in allConnections)
                    {
                        c.SendAsync(data);
                    }
                }
            }
            catch (Exception exc)
            {
                logger.LogError("Failed to stop session with ID " + sessionToken.SessionId + " (" + sessionToken.UserName + "): " + exc);
            }
        }
    }

    public class GameInstance : IDisposable
    {
        private readonly GameData gameData;
        public readonly SessionToken SessionToken;
        private readonly GameProcessor gameProcessor;
        private readonly Thread updateThread;
        private readonly ILogger logger;

        private bool disposed;
        private bool started;

        public GameInstance(
            SessionToken sessionToken,
            IRavenBotApiClient ravenbotApi,
            ITwitchExtensionConnectionProvider extWsProvider,
            ITcpSocketApiConnectionProvider tcpConnectionProvider,
            SessionManager sessionManager,
            PlayerInventoryProvider inventoryProvider,
            GameData gameData,
            ILogger logger)
        {
            this.gameData = gameData;
            this.SessionToken = sessionToken;

            logger.LogInformation($"Instantiating GameInstance for session {sessionToken.UserName}");

            // Check if user exists, if not, create it.
            var user = gameData.GetUser(sessionToken.UserId);
            if (user == null)
            {
                logger.LogWarning($"User '{sessionToken.UserName}' not found, creating user from session token.");
                user = CreateUserFromSessionToken(sessionToken);
            }

            this.gameProcessor = new GameProcessor(
                logger,
                ravenbotApi, extWsProvider, tcpConnectionProvider, sessionManager,
                inventoryProvider, gameData, sessionToken);

            this.logger = logger;

            updateThread = new System.Threading.Thread(UpdateProcess);
        }

        public bool IsActive => !disposed && started;

        private User CreateUserFromSessionToken(SessionToken sessionToken)
        {
            var resources = new Resources
            {
                Id = Guid.NewGuid(),
            };

            var user = new User
            {
                Id = sessionToken.UserId,
                Created = DateTime.UtcNow,
                Resources = resources.Id,
                UserName = sessionToken.UserName,
                DisplayName = sessionToken.DisplayName,
            };

            gameData.Add(resources);
            gameData.Add(user);
            gameData.Add(new UserAccess
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Platform = "twitch",
                PlatformId = sessionToken.TwitchUserId,
                PlatformUsername = sessionToken.TwitchUserName,
                Created = DateTime.UtcNow
            });

            return user;
        }

        public void Start()
        {
            if (started)
            {
                return;
            }

            started = true;
            updateThread.Start();
        }

        private void UpdateProcess()
        {
            using (var cts = new CancellationTokenSource())
            {
                while (!this.disposed && started)
                {
                    if (this.disposed)
                    {
                        cts.Cancel();
                        return;
                    }

                    if (SessionToken == null || SessionToken.Expired)
                    {
                        logger.LogError("[" + SessionToken?.UserName + "] Session Token Expried. Closing WebSocket Connection.");
                        Dispose();
                        return;
                    }

                    try
                    {
                        gameProcessor.Process(cts);
                    }

                    catch (Exception exc)
                    {
                        logger.LogError("[" + SessionToken.UserName + "] Error processing game update: " + exc.ToString());
                        System.Threading.Thread.Sleep(500);
                    }
                    System.Threading.Thread.Sleep(100);
                }

                logger.LogWarning("[" + SessionToken.UserName + "] Session terminated game loop (" + SessionToken.SessionId + ")");
            }
        }

        public void Dispose()
        {
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;
            this.started = false;

            // the loop disposes itself when its token expires: joining its own thread would never return; and from
            // elsewhere (a socket's disconnect callback) it is waited on only briefly, not for as long as a tick takes
            if (Thread.CurrentThread != updateThread && updateThread.IsAlive)
            {
                updateThread.Join(TimeSpan.FromSeconds(2));
            }

            logger.LogWarning("[" + SessionToken.TwitchUserName + "] Session disposed (" + SessionToken.SessionId + ")");
        }
    }
}

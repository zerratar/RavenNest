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
        void Start(SessionToken sessionToken);
        void Stop(SessionToken sessionToken);
    }

    public class GameProcessorManager : IGameProcessorManager
    {
        private readonly ConcurrentDictionary<Guid, GameInstance> gameInstances = new ConcurrentDictionary<Guid, GameInstance>();
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
            foreach (var i in gameInstances)
            {
                if (i.Value != null)
                {
                    i.Value.Dispose();
                }
            }
        }

        public void Start(RavenNest.Models.SessionToken sessionToken)
        {
            var uid = sessionToken.UserId;
            if (gameInstances.TryGetValue(uid, out var existing) && existing != null && existing.IsActive)
            {
                //logger.LogWarning("An existing Game Instance for user with ID " + uid + " exists, we may have two connections now. But we can't have multiple processors.");
                return;
            }

            var state = gameData.GetSessionState(sessionToken.SessionId);
            if (state != null)
            {
                state.IsConnectedToClient = true;
            }

            // do something
            var instance = new GameInstance(sessionToken, ravenbotApi, extWsProvider, tcpConnectionProvider, sessionManager, inventoryProvider, gameData, logger);
            gameInstances[uid] = instance;
            instance.Start();

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

        public void Stop(SessionToken sessionToken)
        {
            try
            {
                if (sessionToken == null)
                {
                    return;
                }

                var uid = sessionToken.UserId;
                if (!gameInstances.TryGetValue(uid, out var instance))
                {
                    return;
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


                gameInstances.Remove(uid, out _);
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
            this.updateThread.Join();

            logger.LogWarning("[" + SessionToken.TwitchUserName + "] Session disposed (" + SessionToken.SessionId + ")");
        }
    }
}

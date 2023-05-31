using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Game.Processors;
using RavenNest.Models;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Threading;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Game;
using RavenNest.BusinessLogic.Twitch.Extension;

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

        public void Start(SessionToken sessionToken)
        {
            var uid = sessionToken.UserId;
            if (gameInstances.TryGetValue(uid, out var existing) && existing != null && existing.IsActive)
            {
                logger.LogWarning("An existing Game Instance for user with ID " + uid + " exists, we may have two connections now. But we can't have multiple processors.");
                return;
            }

            // do something
            var instance = new GameInstance(sessionToken, ravenbotApi, extWsProvider, tcpConnectionProvider, sessionManager, inventoryProvider, gameData, logger);
            gameInstances[uid] = instance;
            instance.Start();
        }

        public void Stop(SessionToken sessionToken)
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

            // do something
            instance.Dispose();
            gameInstances[uid] = null;
        }
    }

    public class GameInstance : IDisposable
    {
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
            this.SessionToken = sessionToken;

            this.gameProcessor = new GameProcessor(
                ravenbotApi, extWsProvider, tcpConnectionProvider, sessionManager,
                inventoryProvider, gameData, sessionToken);

            this.logger = logger;


            updateThread = new System.Threading.Thread(UpdateProcess);

        }

        public bool IsActive => !disposed && started;


        public void Start()
        {
            started = true;
            updateThread.Start();
        }

        private async void UpdateProcess()
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
                        await gameProcessor.ProcessAsync(cts).ConfigureAwait(false);
                    }

                    catch (Exception exc)
                    {
                        logger.LogError("[" + SessionToken.UserName + "] Error processing game update: " + exc.ToString());
                        await Task.Delay(500);
                    }
                    await Task.Delay(16);
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
            this.updateThread.Join();

            logger.LogWarning("[" + SessionToken.TwitchUserName + "] Session disposed (" + SessionToken.SessionId + ")");
        }
    }
}

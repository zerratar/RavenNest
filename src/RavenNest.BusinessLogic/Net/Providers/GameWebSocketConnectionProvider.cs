using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Game;
using RavenNest.BusinessLogic.Game.Processors;
using RavenNest.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Providers;
using RavenNest.BusinessLogic.Twitch.Extension;

namespace RavenNest.BusinessLogic.Net
{
    public class GameWebSocketConnectionProvider : IGameWebSocketConnectionProvider
    {
        private readonly ILogger logger;
        private readonly IRavenBotApiClient ravenBotApi;
        private readonly IIntegrityChecker integrityChecker;
        private readonly IPlayerInventoryProvider inventoryProvider;
        private readonly IExtensionWebSocketConnectionProvider extWsProvider;
        private readonly ITcpSocketApiConnectionProvider tcpConnectionProvider;
        private readonly IGameData gameData;
        private readonly IGameManager gameManager;
        private readonly IGamePacketManager packetManager;
        private readonly IGamePacketSerializer packetSerializer;
        private readonly ISessionManager sessionManager;

        private readonly ConcurrentDictionary<Guid, IGameWebSocketConnection> socketSessions
            = new ConcurrentDictionary<Guid, IGameWebSocketConnection>();

        public GameWebSocketConnectionProvider(
            ILogger<GameWebSocketConnectionProvider> logger,
            IRavenBotApiClient ravenBotApi,
            IIntegrityChecker integrityChecker,
            IPlayerInventoryProvider inventoryProvider,
            IExtensionWebSocketConnectionProvider extWsProvider,
            ITcpSocketApiConnectionProvider tcpConnectionProvider,
            IGameData gameData,
            IGameManager gameManager,
            IGamePacketManager packetManager,
            IGamePacketSerializer packetSerializer,
            ISessionManager sessionManager)
        {
            this.logger = logger;
            this.ravenBotApi = ravenBotApi;
            this.integrityChecker = integrityChecker;
            this.inventoryProvider = inventoryProvider;
            this.extWsProvider = extWsProvider;
            this.tcpConnectionProvider = tcpConnectionProvider;
            this.gameData = gameData;
            this.gameManager = gameManager;
            this.packetManager = packetManager;
            this.packetSerializer = packetSerializer;
            this.sessionManager = sessionManager;
        }

        public IGameWebSocketConnection Get(WebSocket ws, IReadOnlyDictionary<string, string> requestHeaders)
        {
            if (!requestHeaders.TryGetValue("session-token", out var token))
            {
                logger.LogWarning("No Session Token when trying to create websocket connection!");
                return null;
            }

            var sessionToken = sessionManager.Get(token);
            if (!CheckSessionTokenValidity(sessionToken))
            {
                logger.LogWarning("Invalid session token for websocket connection (" + (sessionToken?.SessionId.ToString() ?? "Token Unavailble") + ")");
                return null;
            }

            var session = new WebSocketConnection(
                logger,
                ravenBotApi,
                integrityChecker,
                inventoryProvider,
                gameData,
                gameManager,
                packetManager,
                packetSerializer,
                sessionManager,
                extWsProvider,
                tcpConnectionProvider,
                this,
                ws,
                sessionToken);

#if DEBUG
            logger.LogDebug("[" + sessionToken.TwitchUserName + "] WebSocket Connection Established.");
#endif

            return socketSessions[sessionToken.SessionId] = session.Start();
        }

        public void KillAllConnections()
        {
            try
            {
                var connections = socketSessions.Values;
                foreach (var item in connections)
                {
                    item.Dispose();
                    this.Disconnected(item as WebSocketConnection);
                }
            }
            catch (Exception exc)
            {
                logger.LogError(exc.ToString());
            }
        }

        public bool TryGet(Guid sessionId, out IGameWebSocketConnection session)
        {
            return socketSessions.TryGetValue(sessionId, out session);
        }

        public bool TryGet(SessionToken token, out IGameWebSocketConnection session)
        {
            return socketSessions.TryGetValue(token.SessionId, out session);
        }

        private void Disconnected(WebSocketConnection connection)
        {
            this.socketSessions.Remove(connection.SessionId, out _);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool CheckSessionTokenValidity(SessionToken sessionToken)
        {
            return sessionToken != null && sessionToken.SessionId != Guid.Empty && !sessionToken.Expired;
        }

        private class WebSocketConnection : IGameWebSocketConnection
        {
            private readonly ILogger logger;
            private readonly IGameProcessor gameProcessor;
            private readonly IGamePacketManager packetManager;
            private readonly IGamePacketSerializer packetSerializer;
            private readonly ISessionManager sessionManager;
            private readonly GameWebSocketConnectionProvider sessionProvider;

            private readonly WebSocket ws;
            private readonly TaskCompletionSource<object> killTask;

            private readonly Thread receiveLoop;
            private readonly Thread sendLoop;
            private readonly Thread gameLoop;

            private readonly SessionToken sessionToken;
            private PartialGamePacket unfinishedPacket;
            private bool disposed;

            private readonly ConcurrentDictionary<Guid, TaskCompletionSource<object>> messageLookup
                = new ConcurrentDictionary<Guid, TaskCompletionSource<object>>();

            private readonly SemaphoreSlim readMutex = new SemaphoreSlim(1);
            private readonly ConcurrentQueue<Data> writeQueue = new ConcurrentQueue<Data>();
            public WebSocketConnection(
                ILogger logger,
                IRavenBotApiClient ravenBotApi,
                IIntegrityChecker integrityChecker,
                IPlayerInventoryProvider inventoryProvider,
                IGameData gameData,
                IGameManager gameManager,
                IGamePacketManager packetManager,
                IGamePacketSerializer packetSerializer,
                ISessionManager sessionManager,
                IExtensionWebSocketConnectionProvider extWsProvider,
                ITcpSocketApiConnectionProvider tcpConnectionProvider,
                GameWebSocketConnectionProvider sessionProvider,
                WebSocket ws,
                SessionToken sessionToken)
            {
                this.receiveLoop = new Thread(ReceiveLoop);
                this.sendLoop = new Thread(SendLoop);
                this.gameLoop = new Thread(GameUpdateLoop);
                this.logger = logger;
                this.sessionToken = sessionToken;
                this.packetManager = packetManager;
                this.packetSerializer = packetSerializer;
                this.sessionManager = sessionManager;
                this.sessionProvider = sessionProvider;
                this.ws = ws;

                this.killTask = new TaskCompletionSource<object>();
                this.gameProcessor = new GameProcessor(
                    ravenBotApi, integrityChecker, this, extWsProvider, tcpConnectionProvider, sessionManager, inventoryProvider, gameData, gameManager, sessionToken);
            }

            internal Guid SessionId => this.sessionToken.SessionId;
            public SessionToken SessionToken => sessionToken;

            internal IGameWebSocketConnection Start()
            {
                this.receiveLoop.Start();
                this.sendLoop.Start();
                this.gameLoop.Start();

                return this;
            }

            public async Task<TResult> SendAsync<TResult>(string id, object request, CancellationToken cancellationToken)
            {
                try
                {
                    if (request == null) return default;
                    if (!CanSendData())
                        return default;

                    var packet = CreatePacket(id, request);
                    var taskSource = new TaskCompletionSource<object>();
                    messageLookup[packet.CorrelationId] = taskSource;

                    var bytes = packetSerializer.Serialize(packet);
                    SendDataAsync(bytes, cancellationToken);
                    return (TResult)await taskSource.Task;
                }
                catch (Exception exc)
                {
                    this.logger.LogError("[" + SessionToken.TwitchUserName + "] (" + sessionToken.SessionId + ") " + exc.ToString());
                }

                return default;
            }

            public async Task<bool> PushAsync(string id, object request, CancellationToken cancellationToken)
            {
                try
                {
                    if (!CanSendData())
                        return false;

                    var packet = CreatePacket(id, request);
                    var bytes = packetSerializer.Serialize(packet);
                    SendDataAsync(bytes, cancellationToken);
                    return true;
                }
                catch (Exception exc)
                {
                    this.logger.LogError("[" + SessionToken.TwitchUserName + "] (" + sessionToken.SessionId + ") " + exc.ToString());
                }
                return false;
            }


            public Task ReplyAsync(GamePacket packet, object request)
            {
                return ReplyAsync(packet.CorrelationId, packet.Type, request, CancellationToken.None);
            }

            public Task ReplyAsync(GamePacket packet, object request, CancellationToken cancellationToken)
            {
                return ReplyAsync(packet.CorrelationId, packet.Type, request, cancellationToken);
            }

            public async Task ReplyAsync(Guid correlationId, string id, object request, CancellationToken cancellationToken)
            {
                try
                {
                    if (!CanSendData())
                        return;

                    var packet = CreatePacket(id, request, correlationId);
                    var bytes = packetSerializer.Serialize(packet);
                    SendDataAsync(bytes, cancellationToken);
                }
                catch (Exception exc)
                {
                    this.logger.LogError("[" + SessionToken.TwitchUserName + "] (" + sessionToken.SessionId + ") " + exc.ToString());
                }
            }

            private void SendDataAsync(byte[] bytes, CancellationToken cancellationToken)
            {
                try
                {
                    writeQueue.Enqueue(new Data
                    {
                        Binary = new ArraySegment<byte>(bytes),
                        CancellationToken = cancellationToken
                    });
                }
                catch (Exception exc)
                {
                    this.logger.LogError("[" + SessionToken.TwitchUserName + "] (" + sessionToken.SessionId + ") " + exc.ToString());
                }
            }
            public void Dispose()
            {
                if (this.disposed)
                {
                    return;
                }

                TryCloseConnection();

                this.killTask.TrySetResult(null);
                this.sessionProvider.Disconnected(this);
                this.disposed = true;
                this.receiveLoop.Join();
                this.sendLoop.Join();
                this.gameLoop.Join();
                this.ws.Dispose();

                logger.LogWarning("[" + SessionToken.TwitchUserName + "] Session websocket disposed (" + sessionToken.SessionId + ")");
            }

            private void TryCloseConnection()
            {
                try
                {
                    if (ws == null || this.disposed)
                    {
                        return;
                    }

                    if (ws.CloseStatus != null)
                    {
                        return;
                    }

                    if (ws.State != WebSocketState.Open)
                    {
                        return;
                    }

                    ws.CloseAsync(WebSocketCloseStatus.InvalidPayloadData, "", CancellationToken.None);
                }
                catch (Exception exc)
                {
                    logger.LogInformation("[" + SessionToken.TwitchUserName + "] Unable to close socket connection: " + exc);
                }
            }

            private async void GameUpdateLoop()
            {
                using (var cts = new CancellationTokenSource())
                {
                    while (!this.disposed)
                    {
                        if (this.disposed)
                        {
                            cts.Cancel();
                            return;
                        }

                        if (SessionToken == null || SessionToken.Expired)
                        {
                            logger.LogError("[" + SessionToken?.TwitchUserName + "] Session Token Expried. Closing WebSocket Connection.");
                            Dispose();
                            return;
                        }

                        try
                        {
                            await gameProcessor.ProcessAsync(cts).ConfigureAwait(false);
                        }
                        catch (Exception exc)
                        {
                            logger.LogError("[" + SessionToken.TwitchUserName + "] Error processing game update: " + exc.ToString());
                            await Task.Delay(500);
                        }

                        await Task.Delay(10);
                    }

                    logger.LogWarning("[" + SessionToken.TwitchUserName + "] Session terminated game loop (" + sessionToken.SessionId + ")");
                }
            }

            private async void SendLoop()
            {
                using (var cts = new CancellationTokenSource())
                {
                    while (!this.disposed)
                    {
                        if (this.disposed)
                        {
                            cts.Cancel();
                            return;
                        }

                        if (SessionToken == null || SessionToken.Expired)
                        {
                            logger.LogError("[" + SessionToken?.TwitchUserName + "] Session Token Expried. Closing WebSocket Connection.");
                            Dispose();
                            return;
                        }

                        try
                        {
                            if (writeQueue.TryDequeue(out var packet))
                            {
                                await ws.SendAsync(packet.Binary, WebSocketMessageType.Binary, true, packet.CancellationToken);
                            }
                            else
                            {
                                await Task.Delay(15);
                            }
                        }
                        catch (Exception exc)
                        {
                            logger.LogError("[" + SessionToken.TwitchUserName + "] (" + sessionToken.SessionId + ") Error Writing Packet: " + exc);
                        }
                        finally
                        {
                        }

                    }
                }

                logger.LogWarning("[" + SessionToken.TwitchUserName + "] Session terminated websocket send loop (" + sessionToken.SessionId + ")");
            }

            private async void ReceiveLoop()
            {
                using (var cts = new CancellationTokenSource())
                {
                    while (!this.disposed)
                    {
                        if (this.disposed)
                        {
                            cts.Cancel();
                            return;
                        }

                        if (SessionToken == null || SessionToken.Expired)
                        {
                            logger.LogError("[" + SessionToken?.TwitchUserName + "] Session Token Expried. Closing WebSocket Connection.");
                            Dispose();
                            return;
                        }

                        try
                        {
                            await readMutex.WaitAsync();
                            await ReceivePacketsAsync(cts.Token);
                        }
                        catch (Exception exc)
                        {
                            logger.LogError("[" + SessionToken.TwitchUserName + "] (" + sessionToken.SessionId + ") Error Receiving Packet: " + exc);
                        }
                        finally
                        {
                            readMutex.Release();
                        }
                    }
                }

                logger.LogWarning("[" + SessionToken.TwitchUserName + "] Session terminated websocket receive loop (" + sessionToken.SessionId + ")");
            }

            private async Task ReceivePacketsAsync(CancellationToken cancellationToken)
            {
                try
                {
                    var buffer = new byte[8192];
                    var receiveBuffer = new ArraySegment<byte>(buffer);
                    var result = await ws.ReceiveAsync(receiveBuffer, cancellationToken).ConfigureAwait(false);

                    if (result.CloseStatus != null)
                    {
                        logger.LogWarning("[" + SessionToken.TwitchUserName + "] Session terminated close status: " + result.CloseStatus + " - " + result.CloseStatusDescription + "(" + sessionToken.SessionId + ")");
                        this.Dispose();
                        return;
                    }

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        logger.LogWarning("[" + SessionToken.TwitchUserName + "] Session terminated close status: " + result.CloseStatusDescription + "(" + sessionToken.SessionId + ")");
                        this.Dispose();
                        return;
                    }

                    await HandlePacketDataAsync(receiveBuffer.Array, 0, receiveBuffer.Count, result.EndOfMessage);
                }
                catch (WebSocketException s)
                {
                    // ignore websocket exceptions. those are most likely premature disconnections
                    // happens when server restarts/shutsdown or client just lost connection.
                    // nothing important to record really. 

#if DEBUG
                    this.logger.LogError("[" + SessionToken.TwitchUserName + "] (" + SessionToken.SessionId + ") " + s.ToString());
#endif
                    this.Dispose();
                }
                catch (Exception exc)
                {
                    this.logger.LogError("[" + SessionToken.TwitchUserName + "] (" + SessionToken.SessionId + ") " + exc.ToString());
                    this.Dispose();
                }
            }

            private async Task HandlePacketDataAsync(byte[] array, int offset, int count, bool endOfMessage)
            {
                if (!endOfMessage)
                {
                    if (unfinishedPacket == null)
                    {
                        unfinishedPacket = new PartialGamePacket(packetSerializer, array, count);
                    }
                    else
                    {
                        unfinishedPacket.Append(array, count);
                    }
                }
                else
                {
                    GamePacket packet;
                    if (unfinishedPacket != null)
                    {
                        unfinishedPacket.Append(array, count);
                        packet = unfinishedPacket.Build();
                        unfinishedPacket = null;
                    }
                    else
                    {
                        packet = packetSerializer.Deserialize(array, count);
                    }

                    if (messageLookup.TryGetValue(packet.CorrelationId, out var tcs))
                    {
                        tcs.SetResult(packet.Data);
                        return;
                    }

                    await HandleGamePacketAsync(packet);
                }
            }

            private bool CanSendData()
            {
                if (ws == null) return false;
                if (ws.State != WebSocketState.Open && ws.State != WebSocketState.CloseReceived)
                {
                    this.logger.LogInformation("Session with ID " + this.SessionId + ", websocket connection closed.");
                    return false;
                }

                return true;
            }

            private async Task HandleGamePacketAsync(GamePacket packet)
            {
                if (packet is GamePacketContainer container)
                {
                    foreach (var p in container.Packets)
                    {
                        await HandleGamePacketAsync(p);
                    }
                    return;
                }

                if (packetManager.TryGetPacketHandler(packet.Id, out var packetHandler))
                {
                    await packetHandler.HandleAsync(this, packet).ConfigureAwait(false);
                }
                else
                {
                    await packetManager.Default.HandleAsync(this, packet).ConfigureAwait(false);
                }
            }

            private GamePacket CreatePacket(string id, object request, Guid? correlationId = null)
            {
                return new GamePacket
                {
                    Id = id,
                    Type = request.GetType().Name,
                    CorrelationId = correlationId ?? Guid.NewGuid(),
                    Data = request
                };
            }
            public async Task KeepAlive()
            {
                await killTask.Task;
            }

            private struct Data
            {
                public ArraySegment<byte> Binary;
                public CancellationToken CancellationToken;
            }
        }
    }
}

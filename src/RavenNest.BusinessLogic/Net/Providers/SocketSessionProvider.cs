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

namespace RavenNest.BusinessLogic.Net
{
    public class WebSocketConnectionProvider : IWebSocketConnectionProvider
    {
        private readonly ILogger logger;
        private readonly IIntegrityChecker integrityChecker;
        private readonly IGameData gameData;
        private readonly IGameManager gameManager;
        private readonly IGamePacketManager packetManager;
        private readonly IGamePacketSerializer packetSerializer;
        private readonly ISessionManager sessionManager;
        private readonly ConcurrentDictionary<Guid, IWebSocketConnection> socketSessions
            = new ConcurrentDictionary<Guid, IWebSocketConnection>();

        public WebSocketConnectionProvider(
            ILogger<WebSocketConnectionProvider> logger,
            IIntegrityChecker integrityChecker,
            IGameData gameData,
            IGameManager gameManager,
            IGamePacketManager packetManager,
            IGamePacketSerializer packetSerializer,
            ISessionManager sessionManager)
        {
            this.logger = logger;
            this.integrityChecker = integrityChecker;
            this.gameData = gameData;
            this.gameManager = gameManager;
            this.packetManager = packetManager;
            this.packetSerializer = packetSerializer;
            this.sessionManager = sessionManager;
        }

        public IWebSocketConnection Get(WebSocket ws, IReadOnlyDictionary<string, string> requestHeaders)
        {
            if (!requestHeaders.TryGetValue("session-token", out var token))
            {
                return null;
            }

            var sessionToken = sessionManager.Get(token);
            if (!CheckSessionTokenValidity(sessionToken))
            {
                return null;
            }

            var session = new WebSocketConnection(
                logger,
                integrityChecker,
                gameData,
                gameManager,
                packetManager,
                packetSerializer,
                this,
                ws,
                sessionToken);
            socketSessions[sessionToken.SessionId] = session.Start();
            return socketSessions[sessionToken.SessionId];
        }

        public bool TryGet(Guid sessionId, out IWebSocketConnection session)
        {
            return socketSessions.TryGetValue(sessionId, out session);
        }

        public bool TryGet(SessionToken token, out IWebSocketConnection session)
        {
            return socketSessions.TryGetValue(token.SessionId, out session);
        }

        private void Disconnected(WebSocketConnection connection)
        {
            this.socketSessions.Remove(connection.SessionId, out _);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void AssertSessionTokenValidity(SessionToken sessionToken)
        {
            if (sessionToken == null) throw new NullReferenceException(nameof(sessionToken));
            if (sessionToken.SessionId == Guid.Empty) throw new NullReferenceException(nameof(sessionToken.SessionId));
            if (sessionToken.Expired) throw new Exception("Session has expired.");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool CheckSessionTokenValidity(SessionToken sessionToken)
        {
            return sessionToken != null && sessionToken.SessionId != Guid.Empty && !sessionToken.Expired;
        }

        private class WebSocketConnection : IWebSocketConnection
        {
            private readonly ILogger logger;
            private readonly IGameProcessor gameProcessor;
            private readonly IGamePacketManager packetManager;
            private readonly IGamePacketSerializer packetSerializer;
            private readonly WebSocketConnectionProvider sessionProvider;

            private readonly WebSocket ws;
            private readonly TaskCompletionSource<object> killTask;

            private readonly Thread receiveLoop;
            private readonly Thread gameLoop;

            private readonly SessionToken sessionToken;
            private PartialGamePacket unfinishedPacket;
            private bool disposed;

            private readonly ConcurrentDictionary<Guid, TaskCompletionSource<object>> messageLookup
                = new ConcurrentDictionary<Guid, TaskCompletionSource<object>>();

            public WebSocketConnection(
                ILogger logger,
                IIntegrityChecker integrityChecker,
                IGameData gameData,
                IGameManager gameManager,
                IGamePacketManager packetManager,
                IGamePacketSerializer packetSerializer,
                WebSocketConnectionProvider sessionProvider,
                WebSocket ws,
                SessionToken sessionToken)
            {
                this.receiveLoop = new Thread(ReceiveLoop);
                this.gameLoop = new Thread(GameUpdateLoop);
                this.logger = logger;
                this.sessionToken = sessionToken;
                this.packetManager = packetManager;
                this.packetSerializer = packetSerializer;
                this.sessionProvider = sessionProvider;
                this.ws = ws;

                this.killTask = new TaskCompletionSource<object>();
                this.gameProcessor = new GameProcessor(integrityChecker, this, gameData, gameManager, sessionToken);
            }

            internal Guid SessionId => this.sessionToken.SessionId;
            public SessionToken SessionToken => sessionToken;

            internal IWebSocketConnection Start()
            {
                this.receiveLoop.Start();
                this.gameLoop.Start();
                return this;
            }

            public async Task<TResult> SendAsync<TResult>(string id, object request, CancellationToken cancellationToken)
            {
                try
                {
                    if (request == null) return default;
                    var packet = CreatePacket(id, request);
                    var taskSource = new TaskCompletionSource<object>();
                    messageLookup[packet.CorrelationId] = taskSource;

                    var bytes = packetSerializer.Serialize(packet);
                    await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Binary, true, cancellationToken);
                    return (TResult)await taskSource.Task;
                }
                catch (Exception exc)
                {
                    this.logger.LogError(exc.ToString());
                }
                return default;
            }
            public async Task<bool> PushAsync(string id, object request, CancellationToken cancellationToken)
            {
                var packet = CreatePacket(id, request);
                try
                {
                    var bytes = packetSerializer.Serialize(packet);
                    var data = new ArraySegment<byte>(bytes);
                    await ws.SendAsync(data, WebSocketMessageType.Binary, true, cancellationToken);
                    return true;
                }
                catch (Exception exc)
                {
                    this.logger.LogError(exc.ToString());
                }

                return false;
            }

            public Task ReplyAsync(Guid correlationId, string id, object request, CancellationToken cancellationToken)
            {
                var packet = CreatePacket(id, request);
                packet.CorrelationId = correlationId;

                try
                {
                    var bytes = packetSerializer.Serialize(packet);
                    return ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Binary, true, cancellationToken);
                }
                catch (Exception exc)
                {
                    this.logger.LogError(exc.ToString());
                }

                return Task.CompletedTask;
            }
            public void Dispose()
            {
                if (this.disposed)
                {
                    return;
                }
                this.killTask.TrySetResult(null);
                this.sessionProvider.Disconnected(this);
                this.disposed = true;
                this.receiveLoop.Join();
                this.gameLoop.Join();
                this.ws.Dispose();
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

                        try
                        {
                            await gameProcessor.ProcessAsync(cts);
                        }
                        catch
                        {

                        }

                        await Task.Delay(500);
                    }
                }
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

                        await ReceivePacketsAsync(cts.Token);

                        await Task.Delay(100);
                    }
                }
            }

            private async Task ReceivePacketsAsync(CancellationToken cancellationToken)
            {
                try
                {
                    var buffer = new byte[4096];
                    var receiveBuffer = new ArraySegment<byte>(buffer);
                    var result = await ws.ReceiveAsync(receiveBuffer, cancellationToken);

                    if (result.CloseStatus != null)
                    {
                        this.Dispose();
                        return;
                    }

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        this.Dispose();
                        return;
                    }

                    await HandlePacketDataAsync(receiveBuffer.Array, 0, receiveBuffer.Count, result.EndOfMessage);
                }
                catch (Exception exc)
                {
                    this.logger.LogError(exc.ToString());
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

            private async Task HandleGamePacketAsync(GamePacket packet)
            {
                if (packetManager.TryGetPacketHandler(packet.Id, out var packetHandler))
                {
                    await packetHandler.HandleAsync(this, packet);
                }
                else
                {
                    await packetManager.Default.HandleAsync(this, packet);
                }
            }

            private GamePacket CreatePacket(string id, object request)
            {
                return new GamePacket
                {
                    Id = id,
                    Type = request.GetType().Name,
                    CorrelationId = Guid.NewGuid(),
                    Data = request
                };
            }
            public async Task KeepAlive()
            {
                await killTask.Task;
            }
        }
    }
}
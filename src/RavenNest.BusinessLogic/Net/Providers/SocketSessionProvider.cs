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
using Microsoft.Extensions.Options;
using System.Diagnostics;
using RavenNest.Models.TcpApi;
using MessagePack;
using System.Linq;

namespace RavenNest.BusinessLogic.Net
{
    // Used for external resources to access a tcp api connection
    public interface ITcpSocketApiConnectionProvider
    {
        void Add(int connectionId, TcpSocketApi server);
        bool TryGet(int connectionId, out TcpSocketApiConnection connection);
        bool TryGet(Guid sessionId, out TcpSocketApiConnection connection);
        bool Contains(int connectionId);
        bool Remove(int connectionId);
    }

    public class TcpSocketApiConnectionProvider : ITcpSocketApiConnectionProvider
    {
        private readonly Dictionary<int, TcpSocketApiConnection> connections = new Dictionary<int, TcpSocketApiConnection>();
        private readonly object syncRoot = new object();
        public void Add(int connectionId, TcpSocketApi server)
        {
            lock (syncRoot)
            {
                connections[connectionId] = new TcpSocketApiConnection(connectionId, server);
            }
        }

        public bool Contains(int connectionId)
        {
            lock (syncRoot)
                return connections.ContainsKey(connectionId);
        }

        public bool Remove(int connectionId)
        {
            lock (syncRoot)
                return connections.Remove(connectionId);
        }

        public bool TryGet(int connectionId, out TcpSocketApiConnection connection)
        {
            lock (syncRoot)
                return connections.TryGetValue(connectionId, out connection);

        }
        public bool TryGet(Guid sessionId, out TcpSocketApiConnection connection)
        {
            lock (syncRoot)
            {
                connection = connections.Values.FirstOrDefault(x => x.SessionToken != null && x.SessionToken.SessionId == sessionId);
                return connection != null;
            }
        }
    }

    public class TcpSocketApiConnection
    {
        private readonly int connectionId;
        private TcpSocketApi server;

        public TcpSocketApiConnection(int connectionId, TcpSocketApi server)
        {
            this.connectionId = connectionId;
            this.server = server;
        }
        public SessionToken SessionToken { get; set; }
        public bool Connected => server.IsConnected(this.connectionId);
        public void Send<T>(T model)
        {
            // send a separate packet first with the incoming model type name?
            // or just let it be?

            // currently we should only be sending Models.PlayerRestedUpdate, but this may change in the future.
            server.Send(connectionId, MessagePackSerializer.Serialize(model, MessagePack.Resolvers.ContractlessStandardResolver.Options));
        }
    }

    public class TcpSocketApi : ITcpSocketApi
    {
        public const int MaxMessageSize = 16 * 1024;
        public const int DefaultServerPort = 3920;
        public const int ServerRefreshRate = 60;

        private readonly IOptions<AppSettings> settings;
        private readonly ILogger<TcpSocketApi> logger;
        private readonly ITcpSocketApiConnectionProvider connectionProvider;
        private readonly IPlayerManager playerManager;
        private readonly IGameData gameData;
        private readonly IGamePacketManager packetManager;
        private readonly IGamePacketSerializer packetSerializer;
        private readonly ISessionManager sessionManager;
        private Thread serverThread;

        private int serverPort = DefaultServerPort;
        private Telepathy.Server server;
        private bool running;
        private bool disposed;

        static long messagesReceived = 0;
        static long dataReceived = 0;

        private readonly object clientMutex = new object();

        public TcpSocketApi(
            IOptions<AppSettings> settings,
            ILogger<TcpSocketApi> logger,
            ITcpSocketApiConnectionProvider connectionProvider,
            IPlayerManager playerManager,
            IGameData gameData,
            IGamePacketManager packetManager,
            IGamePacketSerializer packetSerializer,
            ISessionManager sessionManager)
        {
            this.settings = settings;
            this.logger = logger;
            this.connectionProvider = connectionProvider;
            this.playerManager = playerManager;
            this.gameData = gameData;
            this.packetManager = packetManager;
            this.packetSerializer = packetSerializer;
            this.sessionManager = sessionManager;

            if (settings.Value.TcpApiPort > 0)
            {
                serverPort = settings.Value.TcpApiPort;
            }

            Start();
        }

        public bool IsConnected(int connectionId)
        {
            return connectionProvider.Contains(connectionId);
        }

        private void StartInternal()
        {
            if (server != null && running)
            {
                return;
            }

            dataReceived = 0;
            messagesReceived = 0;
            var started = false;
            try
            {
                server = new Telepathy.Server(MaxMessageSize);
                server.OnConnected = OnClientConnected;
                server.OnData = (id, data) => OnData(id, data);
                server.OnDisconnected = OnClientDisconnected;
                server.Start(serverPort);
                running = true;
                started = true;
                logger.LogDebug("TCP API Server started on port " + serverPort);

                Stopwatch stopwatch = Stopwatch.StartNew();

                while (running)
                {
                    // tick and process as many as we can. will auto reply.
                    // (100k limit to avoid deadlocks)
                    server.Tick(100000);

                    // sleep
                    Thread.Sleep(1000 / ServerRefreshRate);

                    // report every 10 seconds
                    if (stopwatch.ElapsedMilliseconds > 10000 && (messagesReceived > 0))
                    {
                        logger.LogDebug(string.Format("Thread[" + Thread.CurrentThread.ManagedThreadId + "]: Server in={0} ({1} KB/s)  out={0} ({1} KB/s) ReceiveQueue={2}", messagesReceived, (dataReceived * 1000 / (stopwatch.ElapsedMilliseconds * 1024)), server.ReceivePipeTotalCount.ToString()));
                        stopwatch.Stop();
                        stopwatch = Stopwatch.StartNew();
                        messagesReceived = 0;
                        dataReceived = 0;
                    }
                }
            }
            catch (Exception exc)
            {
                logger.LogError("Failed to start TCP API Server. " + exc.ToString());
                running = false;
            }

            // if the server did properly start
            // then we want to say that the server stopped.
            if (started)
            {
                logger.LogDebug("TCP API Server stopped.");
            }
        }

        public void Start()
        {
            serverThread = new Thread(StartInternal);
            serverThread.Name = "Tcp Api Server";
            serverThread.IsBackground = true;
            serverThread.Start();
        }

        private void OnClientDisconnected(int connectionId)
        {
            connectionProvider.Remove(connectionId);
            logger.LogDebug(connectionId + " Disconnected");
        }

        public void Send(int connectionId, ArraySegment<byte> message)
        {
            server.Send(connectionId, message);
        }

        private void OnData(int connectionId, ReadOnlyMemory<byte> packetData)
        {
            TcpSocketApiConnection connection = null;
            try
            {
                lock (clientMutex)
                {
                    // we always have one, but don't always have a session token
                    if (!connectionProvider.TryGet(connectionId, out connection))
                    {
                        return;
                    }

                    if (connection.SessionToken != null)
                    {
                        // connection is authenticated. 
                        // we will expect state skill update

                        // check if token is still valid.
                        // Most likely wont be an issue as expiry time is 6 months. LUL.
                        if (!CheckSessionTokenValidity(connection.SessionToken))
                        {
                            logger.LogWarning("Session token expired for tcp api connection (" + (connection?.SessionToken?.SessionId.ToString() ?? "Token Unavailble") + ")");
                            server.Disconnect(connectionId);
                            return;
                        }

                        var update = MessagePackSerializer.Deserialize<CharacterUpdate>(packetData, MessagePack.Resolvers.ContractlessStandardResolver.Options);

                        playerManager.UpdateCharacter(connection.SessionToken, update);
                    }
                    else
                    {
                        // we will expect authentication
                        // if it isnt, we will disconnect the client.
                        // var auth = BinaryPack.BinaryConverter.Deserialize<AuthenticationRequest>(packetData);

                        var auth = MessagePackSerializer.Deserialize<AuthenticationRequest>(packetData, MessagePack.Resolvers.ContractlessStandardResolver.Options);

                        if (string.IsNullOrEmpty(auth.SessionToken))
                        {
                            logger.LogDebug("Connection trying to authenticate with empty session token. Most likely reconnection without sending session token.");
                            server.Disconnect(connectionId);
                            return;
                        }

                        var sessionToken = sessionManager.Get(auth.SessionToken);
                        if (!CheckSessionTokenValidity(sessionToken))
                        {
                            logger.LogWarning("Invalid session token for tcp api connection (" + (sessionToken?.SessionId.ToString() ?? "Token Unavailble") + ")");
                            server.Disconnect(connectionId);
                            return;
                        }

                        connection.SessionToken = sessionToken;
                    }
                }

                //logger.LogDebug(connectionId + " Data: " + BitConverter.ToString(packetData., packetData.Offset, packetData.Count));
            }
            catch (Exception exc)
            {
                if (connection != null)
                {
                    logger.LogError("Failed to handle data from connectionId (" + connectionId + ", session token: " + connection.SessionToken + ") len=" + packetData.Length + ": " + exc.ToString());
                    server.Disconnect(connectionId);
                    return;
                }

                logger.LogError("Failed to handle data from connectionId (" + connectionId + ") len=" + packetData.Length + ": " + exc.ToString());
                server.Disconnect(connectionId);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool CheckSessionTokenValidity(SessionToken sessionToken)
        {
            return sessionToken != null && sessionToken.SessionId != Guid.Empty && !sessionToken.Expired;
        }

        private void OnClientConnected(int connectionId)
        {
            connectionProvider.Add(connectionId, this);
            logger.LogDebug(connectionId + " Connected");
        }

        // TODO: make packetManager handle batch of packets
        //       same with serializer. So we can create a batch of packets to be sent as well.

        // Then for each new connection. Expect client to send session token or disconnect the client if it is not received within moments of connection.
        // Client needs to retry connection if that happens.

        // Come up with a nice port numaber, make sure its open in the router.

        public void Dispose()
        {
            if (server != null)
            {
                try
                {
                    server.Stop();
                }
                catch { }
                server = null;
            }

            this.running = false;
            this.disposed = true;
        }
    }

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

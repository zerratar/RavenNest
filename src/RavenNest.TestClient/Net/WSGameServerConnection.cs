using System.Threading.Tasks;
using System.Threading;
using System.Collections.Concurrent;
using System;
using System.Net.WebSockets;
using RavenNest.BusinessLogic.Net;

namespace RavenNest.TestClient
{
    public class WSGameServerConnection : IGameServerConnection, IDisposable
    {
        private readonly ILogger logger;
        private readonly IAppSettings settings;
        private readonly ITokenProvider tokenProvider;
        private readonly IGamePacketSerializer packetSerializer;

        private readonly ConcurrentDictionary<string, GamePacketHandler> packetHandlers
            = new ConcurrentDictionary<string, GamePacketHandler>();

        private readonly ConcurrentQueue<GamePacket> sendQueue
            = new ConcurrentQueue<GamePacket>();

        private readonly ConcurrentDictionary<Guid, TaskCompletionSource<GamePacket>> awaitedReplies
            = new ConcurrentDictionary<Guid, TaskCompletionSource<GamePacket>>();

        private Thread sendProcessThread;
        private Thread readProcessThread;

        private PartialGamePacket unfinishedPacket;
        private ClientWebSocket webSocket;
        private bool disposed;
        private bool connected;
        private bool connecting;
        private int connectionCounter;

        public int SendAsyncTimeout { get; set; } = 5000;

        public WSGameServerConnection(
            ILogger logger,
            IAppSettings settings,
            ITokenProvider tokenProvider,
            IGamePacketSerializer packetSerializer)
        {
            this.logger = logger;
            this.settings = settings;
            this.tokenProvider = tokenProvider;
            this.packetSerializer = packetSerializer;
        }

        public bool IsReady
        {
            get
            {
                if (!connected)
                {
                    return false;
                }

                if (webSocket == null)
                {
                    return false;
                }

                if (webSocket.CloseStatus != null)
                {
                    return false;
                }

                if (webSocket.State == WebSocketState.Open)
                {
                    return true;
                }

                return false;
            }
        }
        public bool ReconnectRequired => !IsReady && Volatile.Read(ref connectionCounter) > 0;

        public async Task<bool> CreateAsync()
        {
            if (this.connecting || this.connected)
            {
                return false;
            }
            if (disposed) return false;
            this.connecting = true;
            try
            {
                var sessionToken = tokenProvider.GetSessionToken();
                var sessionTokenData = JsonUtility.ToJson(sessionToken).Base64Encode();

                this.webSocket = new ClientWebSocket();
                this.webSocket.Options.SetRequestHeader("session-token", sessionTokenData);
                await this.webSocket.ConnectAsync(new Uri(settings.WebSocketEndpoint), CancellationToken.None);
                connected = true;

                Interlocked.Increment(ref connectionCounter);

                logger.Debug("Connected to the server");

                if (this.readProcessThread == null)
                    (this.readProcessThread = new Thread(ProcessRead)).Start();

                if (this.sendProcessThread == null)
                    (this.sendProcessThread = new Thread(ProcessSend)).Start();

                return true;
            }
            catch (Exception exc)
            {
                logger.Error(exc.Message.ToString());
                connected = false;
            }
            finally
            {
                this.connecting = false;
            }
            return false;
        }

        private async void ProcessRead()
        {
            while (!disposed)
            {
                if (!await this.ReceiveDataAsync())
                {
                    await Task.Delay(1000);
                }
            }
        }

        private async void ProcessSend()
        {
            while (!disposed)
            {
                if (!await this.SendDataAsync())
                {
                    await Task.Delay(1000);
                }
            }
        }

        private async Task<bool> SendDataAsync()
        {
            if (!IsReady) return false;
            if (sendQueue.TryPeek(out var packet))
            {
                try
                {
                    var packetData = packetSerializer.Serialize(packet);
                    var buffer = new ArraySegment<byte>(packetData);
                    await this.webSocket.SendAsync(
                        buffer,
                        WebSocketMessageType.Binary, true, CancellationToken.None);

                    sendQueue.TryDequeue(out _);
                    return true;
                }
                catch (Exception exc)
                {
                    logger.Error(exc.ToString());
                }
            }
            return false;
        }

        private async Task<bool> ReceiveDataAsync()
        {
            if (!IsReady)
            {
                //if (!await CreateAsync())
                return false;
            }

            try
            {
                var buffer = new byte[4096];
                var segment = new ArraySegment<byte>(buffer);
                var result = await this.webSocket.ReceiveAsync(segment, CancellationToken.None);

                if (result.CloseStatus != null || !string.IsNullOrEmpty(result.CloseStatusDescription))
                {
                    this.Disconnect();
                    return false;
                }

                if (!result.EndOfMessage)
                {
                    if (unfinishedPacket == null)
                    {
                        unfinishedPacket =
                            new PartialGamePacket(
                                packetSerializer,
                                segment.Array,
                                result.Count);
                    }
                    else
                    {
                        unfinishedPacket.Append(segment.Array, result.Count);
                    }
                }
                else
                {
                    GamePacket packet = null;
                    if (unfinishedPacket != null)
                    {
                        packet = unfinishedPacket.Build();
                        unfinishedPacket = null;
                    }
                    else
                    {
                        packet = packetSerializer.Deserialize(segment.Array, result.Count);
                    }

                    if (awaitedReplies.TryGetValue(packet.CorrelationId, out var task))
                    {
                        if (task.TrySetResult(packet))
                        {
                            return true;
                        }
                    }

                    await HandlePacketAsync(packet);
                }
                return true;
            }
            catch (Exception exc)
            {
                logger.Error(exc.ToString());

                if (this.webSocket.State == WebSocketState.Aborted)
                    this.Disconnect();

                return false;
            }
        }

        private async Task HandlePacketAsync(GamePacket packet)
        {
            if (packetHandlers.TryGetValue(packet.Id, out var handler))
            {
                await handler.HandleAsync(packet);
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            if (webSocket != null)
            {
                if (IsReady)
                {
                    Disconnect();
                }

                webSocket.Dispose();
            }
            disposed = true;

            readProcessThread.Join();
            sendProcessThread.Join();
        }

        private void Disconnect()
        {
            if (webSocket == null) return;

            connecting = false;
            connected = false;

            if (webSocket.State == WebSocketState.Open)
            {
                webSocket.CloseAsync(
                    WebSocketCloseStatus.Empty,
                    "Closed by client",
                    CancellationToken.None);
            }
            try
            {
                this.webSocket.Dispose();
                this.webSocket = null;
            }
            catch { }

            logger.Debug("Disconnected from server");
        }

        public void Register<TPacketHandler>(
            string packetId,
            TPacketHandler packetHandler) where TPacketHandler : GamePacketHandler
        {
            this.packetHandlers[packetId] = packetHandler;
        }

        public async Task<GamePacket> SendAsync(GamePacket packet)
        {
            var completionSource = new TaskCompletionSource<GamePacket>();

            awaitedReplies[packet.CorrelationId] = completionSource;

            sendQueue.Enqueue(packet);

            await Task.WhenAny(completionSource.Task, Task.Delay(SendAsyncTimeout));

            if (completionSource.Task.IsCompleted)
            {
                return completionSource.Task.Result;
            }

            return null;
        }

        public Task<GamePacket> SendAsync(string id, object model)
        {
            return SendAsync(new GamePacket()
            {
                CorrelationId = Guid.NewGuid(),
                Data = model,
                Id = id,
                Type = model.GetType().Name
            });
        }
    }
}

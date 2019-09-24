using System.Threading.Tasks;
using System.Threading;
using System.Collections.Concurrent;
using System;
using System.Net.WebSockets;
using Newtonsoft.Json;
using System.Text;
using System.Runtime.Serialization;
using MessagePack;
using RavenNest.BusinessLogic.Net;

namespace RavenNest.TestClient
{
    public interface IPlayerController { }
    public interface IGameManager { }

    public interface IWebSocketEndpoint
    {
        Task Update();
        Task<bool> SavePlayerAsync(IPlayerController player);
    }

    internal interface IGameServerConnection
    {
        Task<GamePacket> SendAsync(GamePacket packet);
        Task<GamePacket> SendAsync(string id, object model);

        void Register<TPacketHandler>(string packetId, TPacketHandler packetHandler)
            where TPacketHandler : GamePacketHandler;

        bool IsReady { get; }
        bool ReconnectRequired { get; }

        Task Create();
        Task ReceiveDataAsync();
    }

    public interface ILogger
    {
        void Write(string message);
        void WriteLine(string message);
        void Debug(string message);
        void Error(string errorMessage);
    }

    public interface IAppSettings
    {
        string ApiEndpoint { get; }
        string WebSocketEndpoint { get; }
    }


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

        public async Task Create()
        {
            if (this.connecting || this.connected)
            {
                return;
            }
            if (disposed) return;
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
            }
            catch (Exception exc)
            {
                logger.Error(exc.ToString());
                connected = false;
            }
            finally
            {
                this.connecting = false;
            }
        }

        private async void ProcessRead()
        {
            while (!disposed)
            {
                await this.ReceiveDataAsync();
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

        public async Task ReceiveDataAsync()
        {
            if (!IsReady) return;
            try
            {
                var buffer = new byte[4096];
                var segment = new ArraySegment<byte>(buffer);
                var result = await this.webSocket.ReceiveAsync(segment, CancellationToken.None);

                if (result.CloseStatus != null || !string.IsNullOrEmpty(result.CloseStatusDescription))
                {
                    this.Disconnect();
                    return;
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
                            return;
                        }
                    }

                    await HandlePacketAsync(packet);
                }

            }
            catch (Exception exc)
            {
                logger.Error(exc.ToString());
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

            return await completionSource.Task;
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

    public class WebSocketEndpoint : IWebSocketEndpoint
    {
        private ConcurrentDictionary<string, CharacterStateUpdate> lastSavedState
            = new ConcurrentDictionary<string, CharacterStateUpdate>();

        private readonly IGameServerConnection connection;
        private readonly IGameManager gameManager;
        public WebSocketEndpoint(
            IGameManager gameManager,
            ILogger logger,
            IAppSettings settings,
            ITokenProvider tokenProvider,
            IGamePacketSerializer packetSerializer)
        {
            this.connection = new WSGameServerConnection(
                logger,
                settings,
                tokenProvider,
                packetSerializer);

            this.connection.Register("game_event", new GameEventPacketHandler(gameManager));
            this.gameManager = gameManager;
        }

        public async Task Update()
        {
            if (connection.IsReady)
            {
                return;
            }

            if (connection.ReconnectRequired)
            {
                await Task.Delay(2000);
            }

            await connection.Create();
        }

        public async Task<bool> SavePlayerAsync(IPlayerController player)
        {
            var characterUpdate = new CharacterStateUpdate(
                null,
                0,
                null,
                null,
                false,
                false,
                null,
                null,
                new Vector3
                {
                    x = 0,
                    y = 0,
                    z = 0
                });

            if (lastSavedState.TryGetValue("test", out var lastUpdate))
            {
                if (!RequiresUpdate(lastUpdate, characterUpdate))
                {
                    return false;
                }
            }

            var response = await connection.SendAsync("update_character_state", characterUpdate);
            if (response != null && response.TryGetValue<bool>(out var result))
            {
                if (result)
                {
                    lastSavedState["test"] = characterUpdate;
                    return true;
                }
            }

            return false;
        }

        private bool RequiresUpdate(CharacterStateUpdate oldState, CharacterStateUpdate newState)
        {
            return true;
            //if (oldState.Health != newState.Health) return true;
            //if (oldState.InArena != newState.InArena) return true;
            //if (oldState.InRaid != newState.InRaid) return true;
            //if (oldState.Island != newState.Island) return true;
            ////if (Math.Abs(oldState.Position.magnitude - newState.Position.magnitude) > 0.01) return true;
            //if (oldState.Task != newState.Task) return true;
            //if (oldState.TaskArgument != newState.TaskArgument) return true;
            //return oldState.DuelOpponent != newState.DuelOpponent;
        }
    }


    [MessagePackObject] // only required for messagepack WutFace
    public struct Vector3
    {
        [Key(0)]
        public float x;
        [Key(1)]
        public float y;
        [Key(2)]
        public float z;

        [IgnoreDataMember]
        public float magnitude => x * x + y * y + z * z;
    }

    public class CharacterStateUpdate
    {
        public CharacterStateUpdate(
            string userId,
            int health,
            string island,
            string duelOpponent,
            bool inRaid,
            bool inArena,
            string task,
            string taskArgument,
            Vector3 position)
        {
            UserId = userId;
            Health = health;
            Island = island;
            DuelOpponent = duelOpponent;
            InRaid = inRaid;
            InArena = inArena;
            Task = task;
            TaskArgument = taskArgument;
            Position = position;
        }
        public string UserId { get; }
        public int Health { get; }
        public string Island { get; }
        public string DuelOpponent { get; }
        public bool InRaid { get; }
        public bool InArena { get; }
        public string Task { get; }
        public string TaskArgument { get; }
        public Vector3 Position { get; }
    }

    public abstract class GamePacketHandler
    {
        protected readonly IGameManager GameManager;

        protected GamePacketHandler(IGameManager gameManager)
        {
            this.GameManager = gameManager;
        }
        public abstract Task HandleAsync(GamePacket packet);
    }

    public class GameEventPacketHandler : GamePacketHandler
    {
        public GameEventPacketHandler(IGameManager gameManager)
            : base(gameManager)
        {
        }

        public override Task HandleAsync(GamePacket packet)
        {
            return Task.CompletedTask;
        }
    }

    public static class JsonUtility
    {
        public static string Base64Encode(this string plainText)
        {
            var plainTextBytes = Encoding.UTF8.GetBytes(plainText);
            return System.Convert.ToBase64String(plainTextBytes);
        }

        public static string ToJson(this object obj)
        {
            return JsonConvert.SerializeObject(obj);
        }
    }
}

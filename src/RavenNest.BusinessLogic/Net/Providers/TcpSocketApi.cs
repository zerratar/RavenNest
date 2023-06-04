using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Game;
using RavenNest.Models;
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using RavenNest.Models.TcpApi;
using MessagePack;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;
using TwitchLib.Api.Auth;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace RavenNest.BusinessLogic.Net
{
    public class TcpSocketApi : ITcpSocketApi
    {
        public const int MaxMessageSize = 2 * 256 * 1024;

        public const int MaxMessageSize_v0820 = 16 * 1024;

        public const int DefaultServerPort = 3920;
        public const int ServerRefreshRate = 60;

        private readonly IOptions<AppSettings> settings;
        private readonly ILogger<TcpSocketApi> logger;
        private readonly ITcpSocketApiConnectionProvider connectionProvider;
        private readonly PlayerManager playerManager;
        private readonly GameData gameData;
        private readonly IGameProcessorManager gameProcessorManager;
        private readonly SessionManager sessionManager;
        private Thread serverThread;

        private readonly int serverPort = DefaultServerPort;
        private Telepathy.Server server;
        private bool running;
        private bool disposed;

        static long messagesReceived = 0;
        static long dataReceived = 0;

        private readonly object clientMutex = new object();
        public GameData GameData => gameData;

        public TcpSocketApi(
            IOptions<AppSettings> settings,
            ILogger<TcpSocketApi> logger,
            ITcpSocketApiConnectionProvider connectionProvider,
            PlayerManager playerManager,
            GameData gameData,
            IGameProcessorManager gameProcessorManager,
            SessionManager sessionManager)
        {
            this.settings = settings;
            this.logger = logger;
            this.connectionProvider = connectionProvider;
            this.playerManager = playerManager;
            this.gameData = gameData;
            this.gameProcessorManager = gameProcessorManager;
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
            if (connectionProvider.Remove(connectionId, out var connection))
            {
                gameProcessorManager.Stop(connection.SessionToken);
            }

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

                    if (TryDeserializePacket<SaveExperienceRequest>(packetData, out var saveExp))
                    {
                        if (!HandleSessionToken(saveExp.SessionToken, connection))
                        {
                            return;
                        }

                        playerManager.SaveExperience(connection.SessionToken, saveExp);
                    }
                    else if (TryDeserializePacket<SaveStateRequest>(packetData, out var stateUpdate))
                    {
                        if (!HandleSessionToken(stateUpdate.SessionToken, connection))
                        {
                            return;
                        }

                        playerManager.SaveState(connection.SessionToken, stateUpdate);
                    }
                    else if (TryDeserializePacket<CharacterUpdate>(packetData, out var updatePacket))
                    {
                        // we don't provide a session token in this request. This should be obsolete as its insecure.
                        if (connection.SessionToken == null)
                        {
                            connection.SessionToken = sessionManager.GetSessionTokenByCharacterId(updatePacket.CharacterId);
                        }

                        playerManager.UpdateCharacter(connection.SessionToken, updatePacket);
                    }
                    else if (TryDeserializePacket<AuthenticationRequest>(packetData, out var authPacket))
                    {
                        if (!HandleSessionToken(stateUpdate.SessionToken, connection))
                        {
                            return;
                        }

                        // Now that we have a token
                        //// start the game Game Processor
                        //gameProcessorManager.Start(connection.SessionToken);
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

        private bool HandleSessionToken(string sessionToken, TcpSocketApiConnection connection)
        {
            var token = sessionManager.Get(sessionToken);
            if (!CheckSessionTokenValidity(token))
            {
                logger.LogWarning("Invalid session token for tcp api connection (" + (token?.SessionId.ToString() ?? "Token Unavailble") + ")");
                server.Disconnect(connection.ConnectionId);
                return false;
            }
            if (connection.SessionToken == null)
            {
                connection.SessionToken = token;

                // start the game session if its not already started.
                if (token != null)
                {
                    gameProcessorManager.Start(token);
                }
            }

            return true;
        }

        private static bool TryDeserializePacket<T>(ReadOnlyMemory<byte> packetData, out T value) where T : class
        {
            var options = MessagePack.Resolvers.ContractlessStandardResolver.Options;
            try
            {
                value = MessagePackSerializer.Deserialize<T>(packetData, options);
                return value != null && Validate<T>(value);
            }
            catch
            {
                value = default;
                return false;
            }
        }


        private static T DeserializePacket<T>(ReadOnlyMemory<byte> packetData)
        {
            var options = MessagePack.Resolvers.ContractlessStandardResolver.Options;
            try
            {
                return MessagePackSerializer.Deserialize<T>(packetData, options);
            }
            catch
            {
                return default;
            }
        }

        private static bool Validate<T>(T value) where T : class
        {
            // ugly hax to validate if our packets are correct.

            if (value is SaveExperienceRequest saveRequest)
            {
                return !string.IsNullOrEmpty(saveRequest.SessionToken) && saveRequest.ExpUpdates != null && saveRequest.ExpUpdates.Length > 0;
            }

            if (value is CharacterUpdate characterUpdate)
            {
                return characterUpdate.CharacterId != Guid.Empty && ((characterUpdate.X != 0 || characterUpdate.Y != 0 || characterUpdate.Z != 0) || (characterUpdate.Skills != null && characterUpdate.Skills.Length > 0));
            }

            if (value is AuthenticationRequest tokenRequest)
            {
                return !string.IsNullOrEmpty(tokenRequest.SessionToken);
            }

            if (value is SaveStateRequest stateUpdate)
            {
                return !string.IsNullOrEmpty(stateUpdate.SessionToken) && stateUpdate.StateUpdates != null && stateUpdate.StateUpdates.Length > 0;
            }

            return false;
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

            try
            {
                if (gameProcessorManager != null)
                {
                    gameProcessorManager.Dispose();
                }
            }
            catch { }

            this.running = false;
            this.disposed = true;
        }
    }

    public class PartialByteBuffer
    {
        private byte[] data;
        private readonly int count;

        public PartialByteBuffer(byte[] array, int count)
        {
            this.data = array;
            this.count = count;
        }

        public PartialByteBuffer(ReadOnlyMemory<byte> array)
        {
            this.data = array.ToArray();
            this.count = data.Length;
        }

        public void Append(ReadOnlyMemory<byte> array)
        {
            var tmpArray = new byte[this.count + array.Length];
            Array.Copy(this.data, 0, tmpArray, 0, this.count);
            Array.Copy(array.ToArray(), 0, tmpArray, this.count - 1, array.Length);
            this.data = tmpArray;
        }

        public void Append(ReadOnlyMemory<byte> array, int count)
        {
            var tmpArray = new byte[this.count + count];
            Array.Copy(this.data, 0, tmpArray, 0, this.count);
            Array.Copy(array.ToArray(), 0, tmpArray, this.count - 1, count);
            this.data = tmpArray;
        }

        public T Deserialize<T>()
        {
            return default;
        }

        public static implicit operator ReadOnlyMemory<byte>(PartialByteBuffer input)
        {
            return new ReadOnlyMemory<byte>(input.data);
        }
    }
}

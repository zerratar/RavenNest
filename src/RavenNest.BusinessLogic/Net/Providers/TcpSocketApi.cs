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

namespace RavenNest.BusinessLogic.Net
{
    public class TcpSocketApi : ITcpSocketApi
    {
        public const int MaxMessageSize = 256 * 1024;

        public const int MaxMessageSize_v0820 = 16 * 1024;

        public const int DefaultServerPort = 3920;
        public const int ServerRefreshRate = 60;

        private readonly IOptions<AppSettings> settings;
        private readonly ILogger<TcpSocketApi> logger;
        private readonly ITcpSocketApiConnectionProvider connectionProvider;
        private readonly PlayerManager playerManager;
        private readonly GameData gameData;
        private readonly IGamePacketManager packetManager;
        private readonly IGamePacketSerializer packetSerializer;
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
            IGamePacketManager packetManager,
            IGamePacketSerializer packetSerializer,
            SessionManager sessionManager)
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

                        var update = DeserializePacket<CharacterUpdate>(packetData, connection);
                        if (update != null)
                        {
                            playerManager.UpdateCharacter(connection.SessionToken, update);
                        }
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

        private static T DeserializePacket<T>(ReadOnlyMemory<byte> packetData, TcpSocketApiConnection connection)
        {
            T result = default;
            var options = MessagePack.Resolvers.ContractlessStandardResolver.Options;
            if (packetData.Length == MaxMessageSize)
            {
                // this is probably an unfinished package.
                // but we can't say for certain yet. 
                // try deserializing package
                try
                {
                    result = MessagePackSerializer.Deserialize<T>(packetData, options);
                }
                catch
                {
                    // we are missing data.
                    // lets add the bytes to an unfinished packet for this connection
                    // then hold it until next data comes.
                    if (connection.UnfinishedBuffer != null) // this may be an infinite growing horror.
                    {
                        connection.UnfinishedBuffer.Append(packetData);
                    }
                    else
                    {
                        connection.UnfinishedBuffer = new PartialByteBuffer(packetData);
                    }
                }
            }
            else
            {
                var partial = connection.UnfinishedBuffer;
                if (partial != null)
                {
                    partial.Append(packetData);
                    result = MessagePackSerializer.Deserialize<T>(partial, options);
                    connection.UnfinishedBuffer = null;
                }
                else
                {
                    result = MessagePackSerializer.Deserialize<T>(packetData, options);
                }
            }

            return result;
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

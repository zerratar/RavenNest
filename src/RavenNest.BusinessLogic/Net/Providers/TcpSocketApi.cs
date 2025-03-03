using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using MessagePack;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Game;
using RavenNest.Models;
using RavenNest.Models.TcpApi;

namespace RavenNest.BusinessLogic.Net
{
    /// <summary>
    /// A “typed” packet header so we don’t do repeated TryDeserialize for each message type.
    /// </summary>
    public enum TcpMessageType : byte
    {
        None = 0,
        AuthenticationRequest,
        SaveExperienceRequest,
        SaveStateRequest,
        GameStateRequest,

        // new partial updates.
        PlayerUpdatesBatch
    }

    /// <summary>
    /// Lightweight “envelope” containing the message type, session token, and
    /// the actual payload in a serialized form. This avoids multiple deserialization attempts.
    /// </summary>
    [MessagePackObject]
    public class TypedPacket
    {
        [Key(0)]
        public TcpMessageType MessageType { get; set; }

        // We store the session token at the “envelope” level, so we can validate
        // before fully deserializing the payload (if you want).
        [Key(1)]
        public string SessionToken { get; set; }

        // The “raw” payload. We’ll decode this into the correct struct/class.
        [Key(2)]
        public byte[] Payload { get; set; }

        internal T Deserialize<T>()
        {
            try
            {
                return MessagePackSerializer.Deserialize<T>(
                    Payload,
                    MessagePack.Resolvers.ContractlessStandardResolver.Options
                );
            }
            catch (Exception ex)
            {
                // ignored
            }
            return default;
        }
    }

    public static class TcpSocketApiConstants
    {
        public const int MaxMessageSize = 2_097_152 * 10; // 20 MB, for example
    }

    public class TcpSocketApi : ITcpSocketApi, IDisposable
    {
        private readonly IOptions<AppSettings> settings;
        private readonly ILogger<TcpSocketApi> logger;
        private readonly ITcpSocketApiConnectionProvider connectionProvider;
        private readonly PlayerManager playerManager;
        private readonly GameData gameData;
        private readonly IGameProcessorManager gameProcessorManager;
        private readonly SessionManager sessionManager;

        private readonly int serverPort;
        private Telepathy.Server server;
        private Thread serverThread;
        private volatile bool running;
        private volatile bool disposed;

        private Stopwatch netStatStopwatch = Stopwatch.StartNew();
        private long messagesReceived, dataReceived, messagesSent, dataSent;

        private readonly ConcurrentDictionary<int, string> connectionTokens = new ConcurrentDictionary<int, string>();

        // A thread-safe queue for “real” processing, so the Telepathy
        // loop doesn’t get blocked by heavy logic.
        private readonly BlockingCollection<(int ConnectionId, TypedPacket Packet)> messageQueue
            = new BlockingCollection<(int, TypedPacket)>(new ConcurrentQueue<(int, TypedPacket)>());

        // Simple worker threads to process messages in parallel.
        private Thread[] workerThreads;
        private int workerCount = 4;

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
            workerCount = Math.Max(4, Environment.ProcessorCount);
            serverPort = (settings.Value?.TcpApiPort > 0) ? settings.Value.TcpApiPort : 3920;
            Start();
        }

        public void Start()
        {
            // Start the Telepathy server in a dedicated thread.
            serverThread = new Thread(ServerLoop)
            {
                Name = "TcpApiServerMain",
                IsBackground = true
            };
            running = true;
            serverThread.Start();

            // Start some worker threads to handle the actual messages
            workerThreads = new Thread[workerCount];
            for (int i = 0; i < workerCount; i++)
            {
                workerThreads[i] = new Thread(WorkerLoop)
                {
                    Name = $"TcpApiWorker{i}",
                    IsBackground = true
                };
                workerThreads[i].Start();
            }
        }

        private void ServerLoop()
        {
            try
            {
                server = new Telepathy.Server(RavenNest.BusinessLogic.Net.TcpSocketApiConstants.MaxMessageSize);
                server.OnConnected = OnClientConnected;
                server.OnDisconnected = OnClientDisconnected;
                server.OnData = OnData;
                server.Start(serverPort);

                logger.LogInformation("TCP API Server started on port " + serverPort);

                while (running)
                {
                    server?.Process();
                    ReportNetworkStats();
                    Thread.Sleep(1);
                }
            }
            catch (Exception ex)
            {
                logger.LogError("Failed to start or run TCP server: " + ex);
            }
            finally
            {
                logger.LogInformation("TCP API Server stopped.");
            }
        }

        private void OnClientConnected(int connectionId)
        {
            connectionProvider.Add(connectionId, this, gameData);
            logger.LogDebug($"ConnectionId={connectionId} connected.");
        }

        private void OnClientDisconnected(int connectionId)
        {
            if (connectionProvider.Remove(connectionId, out var connection))
            {
                // Stop any game processor sessions if needed
                gameProcessorManager.Stop(connection.SessionToken);
            }
            logger.LogDebug($"ConnectionId={connectionId} disconnected.");
        }

        private bool TryDeserializePacket<T>(ArraySegment<byte> data, out T packet)
        {
            try
            {
                packet = MessagePackSerializer.Deserialize<T>(
                    data,
                    MessagePack.Resolvers.ContractlessStandardResolver.Options
                );

                return true;
            }
            catch (Exception ex)
            {
                // ignored
            }
            packet = default;
            return false;
        }

        private bool TryDeserializeTypedPacket(ArraySegment<byte> data, out TypedPacket envelope)
        {
            return TryDeserializePacket<TypedPacket>(data, out envelope);
        }
        /// <summary>
        /// Minimal “OnData” that only does lightweight parsing (TypedPacket)
        /// and queues the real logic on the worker threads.
        /// </summary>
        private void OnData(int connectionId, ArraySegment<byte> data)
        {
            Interlocked.Increment(ref messagesReceived);
            Interlocked.Add(ref dataReceived, data.Count);

            // 1) Try to deserialize a “TypedPacket” envelope
            if (!TryDeserializeTypedPacket(data, out var envelope))
            {
                // handle backward compatibility
                if (TryDeserializePacket<AuthenticationRequest>(data, out var auth))
                {
                    connectionTokens[connectionId] = auth.SessionToken;
                    return;
                }

                if (TryDeserializePacket<SaveExperienceRequest>(data, out _))
                {
                    connectionTokens.TryGetValue(connectionId, out var token);
                    envelope = new TypedPacket { MessageType = TcpMessageType.SaveExperienceRequest, Payload = data.ToArray(), SessionToken = token };
                }

                if (TryDeserializePacket<SaveStateRequest>(data, out _))
                {
                    connectionTokens.TryGetValue(connectionId, out var token);
                    envelope = new TypedPacket { MessageType = TcpMessageType.SaveStateRequest, Payload = data.ToArray(), SessionToken = token };
                }

                if (TryDeserializePacket<GameStateRequest>(data, out _))
                {
                    connectionTokens.TryGetValue(connectionId, out var token);
                    envelope = new TypedPacket { MessageType = TcpMessageType.GameStateRequest, Payload = data.ToArray(), SessionToken = token };
                }
            }


            // 2) Quickly verify session token is present (if required).
            //    If it's empty or missing, we might drop immediately.
            if (string.IsNullOrEmpty(envelope.SessionToken))
            {
                logger.LogWarning($"No session token from ConnId={connectionId}. Disconnecting.");
                server?.Disconnect(connectionId);
                return;
            }

            // 3) Enqueue the message for real processing
            messageQueue.Add((connectionId, envelope));
        }

        /// <summary>
        /// This worker loop processes queued messages in parallel. We can do
        /// heavier deserialization or DB writes here without blocking the Telepathy loop.
        /// </summary>
        private void WorkerLoop()
        {
            foreach (var (connectionId, packet) in messageQueue.GetConsumingEnumerable())
            {
                if (!running) break;

                // Validate the connection still exists, etc.
                if (!connectionProvider.TryGet(connectionId, out var tcpConnection))
                {
                    // Possibly disconnected in the meantime
                    continue;
                }

                // Validate the session token
                var token = sessionManager.Get(packet.SessionToken);
                if (!CheckSessionTokenValidity(token))
                {
                    logger.LogWarning($"Invalid session token for ConnId={connectionId}, token={packet.SessionToken}");
                    server?.Disconnect(connectionId);
                    continue;
                }

                // Set the connection’s session token if not already
                if (tcpConnection.SessionToken == null)
                {
                    tcpConnection.SessionToken = token;
                    // Start the game session if not started
                    gameProcessorManager.Start(token);
                }

                // Now handle the typed payload
                switch (packet.MessageType)
                {
                    case TcpMessageType.AuthenticationRequest:
                        // no longer necessary as we send token with all requests.
                        break;

                    case TcpMessageType.SaveExperienceRequest:
                        ProcessSaveExperience(connectionId, token, packet.Deserialize<SaveExperienceRequest>());
                        break;

                    case TcpMessageType.SaveStateRequest:
                        ProcessSaveState(connectionId, token, packet.Deserialize<SaveStateRequest>());
                        break;

                    case TcpMessageType.GameStateRequest:
                        ProcessGameStateRequest(connectionId, token, packet.Deserialize<GameStateRequest>());
                        break;

                    default:
                        logger.LogWarning($"Unknown message type={packet.MessageType} from ConnId={connectionId}.");
                        break;
                }
            }
        }

        private void ProcessSaveExperience(int connectionId, SessionToken token, SaveExperienceRequest saveExpReq)
        {
            try
            {
                playerManager.SaveExperience(token, saveExpReq);
            }
            catch (Exception exc)
            {
                logger.LogError($"ProcessSaveExperience error: {exc}");
            }
        }

        private void ProcessSaveState(int connectionId, SessionToken token, SaveStateRequest saveStateReq)
        {
            try
            {
                playerManager.SaveState(token, saveStateReq);
            }
            catch (Exception exc)
            {
                logger.LogError($"ProcessSaveState error: {exc}");
            }
        }

        private void ProcessGameStateRequest(int connectionId, SessionToken token, GameStateRequest gameStateReq)
        {
            try
            {
                playerManager.SendGameStateToTwitchExtension(token, gameStateReq);
            }
            catch (Exception exc)
            {
                logger.LogError($"ProcessGameStateRequest error: {exc}");
            }
        }

        public void Send(int connectionId, ArraySegment<byte> message)
        {
            server?.Send(connectionId, message);
            Interlocked.Add(ref dataSent, message.Count);
            Interlocked.Increment(ref messagesSent);
        }

        private bool CheckSessionTokenValidity(SessionToken sessionToken)
        {
            return sessionToken != null && sessionToken.SessionId != Guid.Empty && !sessionToken.Expired;
        }

        private void ReportNetworkStats()
        {
            if (netStatStopwatch.ElapsedMilliseconds >= 10_000)
            {
                var msReceived = Interlocked.Read(ref messagesReceived);
                var dtReceived = Interlocked.Read(ref dataReceived);
                var msSent = Interlocked.Read(ref messagesSent);
                var dtSent = Interlocked.Read(ref dataSent);

                var inRateKBps = (dtReceived > 0)
                    ? (dtReceived * 1000.0 / (netStatStopwatch.ElapsedMilliseconds * 1024))
                    : 0;
                var outRateKBps = (dtSent > 0)
                    ? (dtSent * 1000.0 / (netStatStopwatch.ElapsedMilliseconds * 1024))
                    : 0;

                // You can store or log the stats, e.g.:
                gameData.SetNetworkStats(Thread.CurrentThread.ManagedThreadId,
                                         msReceived, inRateKBps,
                                         msSent, outRateKBps);

                logger.LogDebug($"[TCP] in={msReceived} msgs ({inRateKBps:F2} KB/s), out={msSent} msgs ({outRateKBps:F2} KB/s)");

                netStatStopwatch.Restart();
                Interlocked.Exchange(ref messagesReceived, 0);
                Interlocked.Exchange(ref dataReceived, 0);
                Interlocked.Exchange(ref messagesSent, 0);
                Interlocked.Exchange(ref dataSent, 0);
            }
        }

        public bool IsConnected(int connectionId)
        {
            return connectionProvider.Contains(connectionId);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            running = false;

            try
            {
                messageQueue.CompleteAdding();
            }
            catch { }

            // Stop Telepathy
            try
            {
                server?.Stop();
            }
            catch { }
            server = null;

            // Wait for worker threads to exit
            foreach (var wt in workerThreads)
            {
                try { wt.Join(2000); } catch { }
            }

            logger.LogInformation("TcpSocketApi disposed.");
        }
    }
}

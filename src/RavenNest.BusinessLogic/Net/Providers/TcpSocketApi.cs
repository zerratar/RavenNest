using MessagePack;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Game;
using RavenNest.Models;
using RavenNest.Models.TcpApi;
using System;
using System.Collections.Concurrent;
using System.Data;
using System.Diagnostics;
using System.Threading;
using TwitchLib.Api.Helix.Models.Bits;

namespace RavenNest.BusinessLogic.Net
{
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

        // A thread-safe queue for “real” processing, so the Telepathy
        // loop doesn’t get blocked by heavy logic.
        private readonly BlockingCollection<(int ConnectionId, TypedPacket Packet)> messageQueue
            = new BlockingCollection<(int, TypedPacket)>(new ConcurrentQueue<(int, TypedPacket)>());

        // Simple worker threads to process messages in parallel.
        private Thread[] workerThreads;
        private int workerCount = 8;

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
            workerCount = Math.Min(workerCount, Environment.ProcessorCount);
            serverPort = (settings.Value?.TcpApiPort > 0) ? settings.Value.TcpApiPort : 3920;
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
                int serverFrequency = 60;

                server = new Telepathy.Server(RavenNest.BusinessLogic.Net.TcpSocketApiConstants.MaxMessageSize);
                server.OnConnected = OnClientConnected;
                server.OnDisconnected = OnClientDisconnected;
                server.OnData = OnData;
                server.Start(serverPort);

                logger.LogInformation("TCP API Server started on port " + serverPort);

                while (running)
                {
                    if (server == null)
                    {
                        logger.LogError("TCP server is null. Exiting loop.");
                        Dispose();
                        break;
                    }

                    server.Process(100000, logger);
                    ReportNetworkStats();
                    Thread.Sleep(1000 / serverFrequency);
                }

                logger.LogInformation("TCP API Server exiting...");
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
            //logger.LogInformation($"ConnectionId={connectionId} connected.");
        }

        private void OnClientDisconnected(int connectionId)
        {
            if (connectionProvider.Remove(connectionId, out var connection))
            {
                // Stop any game processor sessions if needed
                gameProcessorManager.Stop(connection.SessionToken);
            }
            //logger.LogInformation($"ConnectionId={connectionId} disconnected.");
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
            var res = TryDeserializePacket<TypedPacket>(data, out envelope);
            if (res)
            {
                if (envelope.MessageType == TcpMessageType.None || envelope.Payload == null || envelope.Payload.Length == 0)
                {
                    res = false;
                }
            }
            return res;
        }

        /// <summary>
        /// Minimal “OnData” that only does lightweight parsing (TypedPacket)
        /// and queues the real logic on the worker threads.
        /// </summary>
        private void OnData(int connectionId, ArraySegment<byte> data)
        {
            messagesReceived++;
            dataReceived += data.Count;

            // 1) Try to deserialize a “TypedPacket” envelope
            if (!TryDeserializeTypedPacket(data, out var envelope))
            {
                // handle backward compatibility
                if (TryDeserializePacket<GameStateRequest>(data, out var gameState) && gameState.Dungeon != null)
                {
                    envelope = new TypedPacket
                    {
                        MessageType = TcpMessageType.GameStateRequest,
                        Payload = data.ToArray(),
                        SessionToken = gameState.SessionToken,
                        Object = gameState,
                        Timestamp = DateTimeOffset.UtcNow
                    };
                }
                else if (TryDeserializePacket<SaveExperienceRequest>(data, out var saveExp) && saveExp.ExpUpdates != null)
                {
                    envelope = new TypedPacket
                    {
                        MessageType = TcpMessageType.SaveExperienceRequest,
                        Payload = data.ToArray(),
                        SessionToken = saveExp.SessionToken,
                        Object = saveExp,
                        Timestamp = DateTimeOffset.UtcNow
                    };
                }
                else if (TryDeserializePacket<SaveStateRequest>(data, out var saveState) && saveState.StateUpdates != null)
                {
                    envelope = new TypedPacket
                    {
                        MessageType = TcpMessageType.SaveStateRequest,
                        Payload = data.ToArray(),
                        SessionToken = saveState.SessionToken,
                        Object = saveState,
                        Timestamp = DateTimeOffset.UtcNow
                    };
                }

                else if (TryDeserializePacket<AuthenticationRequest>(data, out var auth))
                {
                    envelope = new TypedPacket
                    {
                        MessageType = TcpMessageType.AuthenticationRequest,
                        Payload = data.ToArray(),
                        SessionToken = auth.SessionToken,
                        Object = auth,
                        Timestamp = DateTimeOffset.UtcNow
                    };
                }
            }

            if (envelope == null)
            {
                logger.LogWarning($"Packet from ConnId={connectionId}. Could not be deserialized!");
                return;
            }

            var now = DateTimeOffset.UtcNow;
            var packetAge = now - envelope.Timestamp;

            var connectionTimeOffset = TimeSpan.MaxValue;
            if (connectionProvider.TryGet(connectionId, out var tcpConnection))
            {
                connectionTimeOffset = tcpConnection.TimeOffset;
            }

            if (packetAge >= TimeSpan.FromMinutes(5))
            {
                logger.LogWarning($"Packet from ConnId={connectionId} is older than 5 minutes and will be ignored! Type={envelope.MessageType}, Timestamp={envelope.Timestamp}, Age={packetAge.TotalSeconds} seconds");
                return;
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
            while (running)
            {
                try
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

#if DEBUG
                        logger.LogDebug($"Processing Packet from ConnId={connectionId}. User={token.UserName}");
#endif

                        // Synchronize to ensure only one thread sets the SessionToken and calls Start
                        lock (tcpConnection)
                        {
                            var offset = DateTime.UtcNow - packet.Timestamp;
                            if (tcpConnection.SessionToken == null)
                            {
                                tcpConnection.SessionToken = token;
                                tcpConnection.TimeOffset = offset;
                                connectionProvider.AttachSessionToken(tcpConnection.ConnectionId, token, offset);
                                gameProcessorManager.Start(token);
                            }

                            if (tcpConnection.TimeOffset > offset)
                            {
                                tcpConnection.TimeOffset = offset;
                            }
                        }

                        // Now handle the typed payload
                        switch (packet.MessageType)
                        {
                            case TcpMessageType.AuthenticationRequest:
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
                catch (Exception exc)
                {
                    logger.LogError($"[TCP] WorkerLoop error: {exc}");
                }

                Thread.Sleep(100);
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
            dataSent += message.Count;
            messagesSent++;
        }

        private bool CheckSessionTokenValidity(SessionToken sessionToken)
        {
            return sessionToken != null && sessionToken.SessionId != Guid.Empty && !sessionToken.Expired;
        }

        private void ReportNetworkStats()
        {
            try
            {
                if (netStatStopwatch.ElapsedMilliseconds >= 10_000)
                {
                    var msReceived = messagesReceived;
                    var dtReceived = dataReceived;
                    var msSent = messagesSent;
                    var dtSent = dataSent;

                    var inRateKBps = (dtReceived > 0)
                        ? (dtReceived * 1000.0 / (netStatStopwatch.ElapsedMilliseconds * 1024))
                        : 0;
                    var outRateKBps = (dtSent > 0)
                        ? (dtSent * 1000.0 / (netStatStopwatch.ElapsedMilliseconds * 1024))
                        : 0;

                    // You can store or log the stats, e.g.:
                    gameData.SetEventServerNetworkStats(
                        Thread.CurrentThread.ManagedThreadId,
                        msReceived, inRateKBps, msSent, outRateKBps);

                    netStatStopwatch.Restart();
                    messagesReceived = 0;
                    dataReceived = 0;
                    messagesSent = 0;
                    dataSent = 0;

                    MessageBus.Shared.Send("OnEventServerStatsUpdated");

                    //logger.LogInformation($"[TCP] in={msReceived} msgs ({inRateKBps:F2} KB/s), out={msSent} msgs ({outRateKBps:F2} KB/s)");
                }
            }
            catch (Exception exc)
            {
                logger.LogError($"ReportNetworkStats error: {exc}");
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

            logger.LogDebug("Disposing TcpSocketApi.");
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
        }
    }
}

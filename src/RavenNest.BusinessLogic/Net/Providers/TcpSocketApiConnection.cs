using RavenNest.Models;
using MessagePack;
using RavenNest.BusinessLogic.Extensions;
using System.Collections.Generic;
using System.Collections.Concurrent;
using RavenNest.BusinessLogic.Data;
using System;

namespace RavenNest.BusinessLogic.Net
{
    public class TcpSocketApiConnection
    {
        private const int MaxQueueSize = 256;
        private int QueueSize = MaxQueueSize;

        private readonly GameData gameData;
        private readonly int connectionId;
        private readonly TcpSocketApi server;

        private DataModels.SessionState sessionState;

        public TcpSocketApiConnection(int connectionId, TcpSocketApi server, GameData gameData)
        {
            this.gameData = gameData;
            this.connectionId = connectionId;
            this.server = server;
            this.Created = DateTime.UtcNow;
        }

        public DateTime Created { get; }
        public int ConnectionId => connectionId;
        public SessionToken SessionToken { get; set; }

        /// <summary>When it signed in; the newest connection of a streamer gets their events.</summary>
        public DateTime Attached { get; set; }
        public bool Connected => server.IsConnected(this.connectionId);

        public TimeSpan TimeOffset { get; set; } = TimeSpan.MaxValue;

        public bool Send<T>(T model)
        {
            // send a separate packet first with the incoming model type name?
            // or just let it be?

            var bytes = MessagePackSerializer.Serialize(model, MessagePack.Resolvers.ContractlessStandardResolver.Options);
            var maxMessageSize = GetMaxMessageSize();
            if (bytes.Length >= maxMessageSize)
            {
                return false;
            }

            return server.Send(connectionId, bytes);
        }

        public int GetMaxMessageSize()
        {
            return TcpSocketApiConstants.MaxMessageSize;
        }

        public bool TryGetClientVersion(out string version)
        {
            if (sessionState != null)
            {
                version = sessionState.ClientVersion;
                return true;
            }

            if (SessionToken != null)
            {
                sessionState = gameData.GetSessionState(SessionToken.SessionId);
                if (sessionState != null)
                {
                    version = sessionState.ClientVersion;
                    return true;
                }
            }

            version = null;
            return false;
        }

        internal void Send(DataModels.GameEvent model)
        {
            try
            {
                var gameEvent = ModelMapper.Map(model);
                Send(new EventList { Events = new List<GameEvent> { gameEvent } });
            }
            catch { }
        }

        /// <summary>
        /// Sends what is waiting for the streamer's client, a batch at a time. Events leave the queue only when the
        /// send is accepted; a batch too big for one message is tried smaller, and a single event too big to ever go
        /// is dropped (logged by the caller) rather than blocking everything behind it. A failure leaves the events for
        /// the next try instead of wiping the queue, as it did before.
        /// </summary>
        /// <returns>How many were sent.</returns>
        internal int Deliver(PendingGameEvents pending, Action<string> log)
        {
            var sent = 0;
            try
            {
                for (var round = 0; round < 8; ++round)
                {
                    var batch = pending.Peek(QueueSize, out var last);
                    if (batch.Count == 0)
                    {
                        break;
                    }

                    var bytes = MessagePackSerializer.Serialize(new EventList { Events = batch }, MessagePack.Resolvers.ContractlessStandardResolver.Options);
                    if (bytes.Length >= GetMaxMessageSize())
                    {
                        if (batch.Count == 1)
                        {
                            log?.Invoke("A game event of type " + batch[0].Type + " is too big to send (" + bytes.Length + " bytes) and is dropped.");
                            pending.RemoveThrough(last);
                            continue;
                        }
                        QueueSize = Math.Max(1, batch.Count / 2);
                        continue;
                    }

                    if (!server.Send(connectionId, bytes))
                    {
                        // the socket is gone or backed up: keep them for the next try
                        break;
                    }

                    pending.RemoveThrough(last);
                    sent += batch.Count;
                    QueueSize = Math.Min(MaxQueueSize, QueueSize * 2);
                    if (batch.Count < QueueSize)
                    {
                        break;
                    }
                }
            }
            catch (Exception exc)
            {
                log?.Invoke("Failed to send game events, they wait for the next try: " + exc.Message);
            }
            return sent;
        }
    }
}

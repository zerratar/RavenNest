﻿using RavenNest.Models;
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
        private const int MinQueueSize = 32;
        private int QueueSize = MaxQueueSize;

        private readonly GameData gameData;
        private readonly int connectionId;
        private readonly ConcurrentQueue<GameEvent> sendQueue = new ConcurrentQueue<GameEvent>();
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

            server.Send(connectionId, bytes);
            return true;
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

        internal void Enqueue(GameEvent model)
        {
            sendQueue.Enqueue(model);
        }

        internal void Enqueue(DataModels.GameEvent model)
        {
            var gameEvent = ModelMapper.Map(model);

            sendQueue.Enqueue(gameEvent);
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

        internal void ProcessSendQueue()
        {
            try
            {
                var toSend = new List<GameEvent>();

                while (toSend.Count < QueueSize && sendQueue.TryDequeue(out var @event))
                {
                    toSend.Add(@event);
                }

                if (toSend.Count > 0)
                {
                    if (!Send(new EventList
                    {
                        Events = toSend
                    }))
                    {
                        // we tried to send too much.
                        // throttle queue. and re-enqueue items.
                        QueueSize = MinQueueSize;
                        foreach (var item in toSend)
                        {
                            Enqueue(item);
                        }
                    }
                }
            }
            catch
            {
                sendQueue.Clear();
            }
        }
    }
}

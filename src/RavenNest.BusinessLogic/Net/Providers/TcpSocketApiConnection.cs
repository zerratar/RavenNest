using RavenNest.Models;
using MessagePack;
using System;
using RavenNest.BusinessLogic.Extensions;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace RavenNest.BusinessLogic.Net
{
    public class TcpSocketApiConnection
    {
        private const int MaxQueueSize = 256;

        private readonly int connectionId;
        private readonly ConcurrentQueue<GameEvent> sendQueue = new ConcurrentQueue<GameEvent>();
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

            // currently we should only be sending RavenNest.Models.PlayerRestedUpdate, but this may change in the future.
            server.Send(connectionId, MessagePackSerializer.Serialize(model, MessagePack.Resolvers.ContractlessStandardResolver.Options));
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

        internal void ProcessSendQueue()
        {
            var toSend = new List<GameEvent>();

            while (toSend.Count < MaxQueueSize && sendQueue.TryDequeue(out var @event))
            {
                toSend.Add(@event);
            }
            if (toSend.Count > 0)
            {
                Send(new EventList
                {
                    Events = toSend
                });
            }
        }
    }
}

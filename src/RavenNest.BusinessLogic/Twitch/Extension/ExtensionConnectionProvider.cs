﻿using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Data;
using RavenNest.Sessions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RavenNest.BusinessLogic.Twitch.Extension
{
    public class ExtensionConnectionProvider : IExtensionWebSocketConnectionProvider
    {
        private readonly IGameData gameData;
        private readonly ISessionInfoProvider sessionInfoProvider;
        private readonly ILogger<ExtensionConnectionProvider> logger;
        private readonly IExtensionPacketDataSerializer packetDataSerializer;
        private readonly Dictionary<string, IExtensionConnection> connections = new Dictionary<string, IExtensionConnection>();

        private readonly object mutex = new object();

        public ExtensionConnectionProvider(
            ILogger<ExtensionConnectionProvider> logger,
            IGameData gameData,
            IExtensionPacketDataSerializer packetDataSerializer,
            ISessionInfoProvider sessionInfoProvider
        )
        {
            this.logger = logger;
            this.gameData = gameData;
            this.packetDataSerializer = packetDataSerializer;
            this.sessionInfoProvider = sessionInfoProvider;
        }

        public IEnumerable<IExtensionConnection> GetAll()
        {
            lock (mutex)
            {
                return connections.Values;
            }
        }

        public IExtensionConnection Get(
            System.Net.WebSockets.WebSocket socket,
            IReadOnlyDictionary<string, string> requestHeaders)
        {
            var sessionId = requestHeaders.GetSessionId();
            if (!sessionInfoProvider.TryGet(sessionId, out var session))
            {
                return null;
            }

            if (socket == null)
            {
                return null;
            }

            lock (mutex)
            {
                var connection = new ExtensionWebSocketConnection(logger, socket, packetDataSerializer, session, requestHeaders["broadcasterId"]);
                connections[sessionId] = connection;
                return connection;
            }
        }

        public bool TryGet(string sessionId, out IExtensionConnection connection)
        {
            lock (mutex)
            {
                return connections.TryGetValue(sessionId, out connection);
            }
        }

        public bool TryGetAllByStreamer(Guid streamerUserId, out IReadOnlyList<IExtensionConnection> connections)
        {
            lock (mutex)
            {
                var streamer = gameData.GetUser(streamerUserId);
                return (connections = this.connections.Where(x => x.Value.BroadcasterTwitchUserId == streamer.UserId).Select(x => x.Value).ToList()).Count > 0;
            }
        }

        public bool TryGetAllByUser(Guid userId, out IReadOnlyList<IExtensionConnection> connections)
        {
            lock (mutex)
            {
                return (connections = this.connections.Where(x => x.Value.Session.Id == userId).Select(x => x.Value).ToList()).Count > 0;
            }
        }

        public bool TryGet(Guid characterId, out IExtensionConnection connection)
        {
            lock (mutex)
            {
                if (sessionInfoProvider.TryGet(characterId, out var session))
                {
                    return TryGet(session.SessionId, out connection);
                }

                connection = null;
                return false;
            }
        }
    }
}
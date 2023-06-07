using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Data;
using RavenNest.DataModels;
using System;
using System.Collections.Generic;
using System.Linq;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace RavenNest.BusinessLogic.Twitch.Extension
{
    public class TwitchExtensionConnectionProvider : ITwitchExtensionConnectionProvider
    {
        private readonly GameData gameData;
        private readonly SessionInfoProvider sessionInfoProvider;
        private readonly ILogger<TwitchExtensionConnectionProvider> logger;
        private readonly IExtensionPacketDataSerializer packetDataSerializer;
        private readonly Dictionary<string, IExtensionConnection> connections = new Dictionary<string, IExtensionConnection>();

        private readonly object mutex = new object();

        public bool Enabled { get; set; } = true;

        public TwitchExtensionConnectionProvider(
            ILogger<TwitchExtensionConnectionProvider> logger,
            GameData gameData,
            IExtensionPacketDataSerializer packetDataSerializer,
            SessionInfoProvider sessionInfoProvider
        )
        {
            this.logger = logger;
            this.gameData = gameData;
            this.packetDataSerializer = packetDataSerializer;
            this.sessionInfoProvider = sessionInfoProvider;
        }

        public IEnumerable<IExtensionConnection> GetAll()
        {
            if (!Enabled) return null;
            lock (mutex)
            {
                return connections.Values;
            }
        }

        public IExtensionConnection Get(
            System.Net.WebSockets.WebSocket socket,
            IReadOnlyDictionary<string, string> requestHeaders)
        {
            if (!Enabled) return null;
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
            if (!Enabled)
            {
                connection = null;
                return false;
            }
            lock (mutex)
            {
                return connections.TryGetValue(sessionId, out connection);
            }
        }

        public bool TryGetAllByStreamer(Guid streamerUserId, out IReadOnlyList<IExtensionConnection> connections)
        {
            if (!Enabled)
            {
                connections = null;
                return false;
            }
            lock (mutex)
            {
                var streamer = gameData.GetUser(streamerUserId);
                return (connections = this.connections.SelectWhere(x => x.Value.BroadcasterTwitchUserId == streamer.UserId, x => x.Value)).Count > 0;
            }
        }

        public bool TryGetAllByUser(Guid userId, out IReadOnlyList<IExtensionConnection> connections)
        {
            if (!Enabled)
            {
                connections = null;
                return false;
            }

            lock (mutex)
            {
                return (connections = this.connections.SelectWhere(x => x.Value.Session.Id == userId, x => x.Value)).Count > 0;
            }
        }

        public bool TryGet(Guid characterId, out IExtensionConnection connection)
        {
            if (!Enabled)
            {
                connection = null;
                return false;
            }

            if (sessionInfoProvider.TryGet(characterId, out var session))
            {
                return TryGet(session.SessionId, out connection);
            }

            connection = null;
            return false;
        }
    }
}

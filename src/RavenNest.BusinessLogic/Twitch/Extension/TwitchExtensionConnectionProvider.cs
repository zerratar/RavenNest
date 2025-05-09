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
            if (!Enabled)
            {
                return null;
            }

            if (requestHeaders == null)
            {
                logger.LogError("Got a websocket request with requestHeaders being null! Extension websocket cannot be created");
                return null;
            }

            var sessionId = requestHeaders.GetSessionId();
            if (!sessionInfoProvider.TryGet(sessionId, out var session))
            {
                logger.LogError("No session for SessionId: " + sessionId + ", extension websocket cannot be created");
                return null;
            }

            if (socket == null)
            {
                logger.LogError("Socket is NULL! For SessionId: " + sessionId + ", extension websocket cannot be created");
                return null;
            }

            lock (mutex)
            {
                try
                {
                    var connection = new ExtensionWebSocketConnection(logger, socket, packetDataSerializer, session, requestHeaders["broadcasterId"]);
                    connections[sessionId] = connection;
                    return connection;
                }
                catch (Exception exc)
                {
                    logger.LogError("Failed to create extension web socket! " + exc);
                    return null;
                }
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
                if (connections.TryGetValue(sessionId, out connection))
                {
                    if(connection.Closed)
                    {
                        connection = null;
                        connections.Remove(sessionId);
                        return false;
                    }
                    return true;
                }

                return false;
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
                if (streamer == null)
                {
                    connections = Array.Empty<IExtensionConnection>();
                    return false;
                }

                var twitch = gameData.GetUserAccess(streamer.Id, "twitch");
                if (twitch == null)
                {
                    connections = Array.Empty<IExtensionConnection>();
                    return false;
                }

                return (connections = this.connections.SelectWhere(x => !x.Value.Closed && x.Value.BroadcasterTwitchUserId == twitch.PlatformId, x => x.Value)).Count > 0;
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
                return (connections = this.connections.SelectWhere(x => 
                    !x.Value.Closed && x.Value.Session.Id == userId, x => x.Value)).Count > 0;
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

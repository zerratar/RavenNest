using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Game;
using RavenNest.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace RavenNest.BusinessLogic.Twitch.Extension
{
    public class ExtensionConnectionProvider : IExtensionWebSocketConnectionProvider
    {
        private readonly ILogger<ExtensionConnectionProvider> logger;
        private readonly IExtensionPacketDataSerializer packetDataSerializer;
        private readonly ISessionManager sessionManager;
        private readonly List<IExtensionConnection> connections = new List<IExtensionConnection>();
        private readonly object mutex = new object();
        public ExtensionConnectionProvider(
            ILogger<ExtensionConnectionProvider> logger,
            IExtensionPacketDataSerializer packetDataSerializer,
            ISessionManager sessionManager)
        {
            this.logger = logger;
            this.packetDataSerializer = packetDataSerializer;
            this.sessionManager = sessionManager;
        }

        public IReadOnlyList<IExtensionConnection> GetAll()
        {
            lock (mutex)
            {
                return connections.ToList();
            }
        }

        public IExtensionConnection Get(System.Net.WebSockets.WebSocket socket, IReadOnlyDictionary<string, string> requestHeaders)
        {
            //if (!requestHeaders.TryGetValue("session-token", out var token))
            //{
            //    logger.LogWarning("No Session Token when trying to create websocket connection!");
            //    return null;
            //}

            //var sessionToken = sessionManager.Get(token);
            //if (!CheckSessionTokenValidity(sessionToken))
            //{
            //    logger.LogWarning("Invalid session token for websocket connection (" + sessionToken.SessionId + ")");
            //    return null;
            //}

            if (socket == null)
            {
                return null;
            }

            lock (mutex)
            {
                var connection = new ExtensionWebSocketConnection(logger, socket, packetDataSerializer);
                connections.Add(connection);
                return connection;
            }
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool CheckSessionTokenValidity(SessionToken sessionToken)
        {
            return sessionToken != null && sessionToken.SessionId != Guid.Empty && !sessionToken.Expired;
        }
    }
}

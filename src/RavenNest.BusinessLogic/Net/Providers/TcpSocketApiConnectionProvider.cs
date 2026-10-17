using RavenNest.BusinessLogic.Data;
using RavenNest.Models;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace RavenNest.BusinessLogic.Net
{
    public class TcpSocketApiConnectionProvider : ITcpSocketApiConnectionProvider
    {
        private readonly ConcurrentDictionary<int, TcpSocketApiConnection> connections = new ConcurrentDictionary<int, TcpSocketApiConnection>();
        private readonly ConcurrentDictionary<Guid, List<TcpSocketApiConnection>> userConnections = new ConcurrentDictionary<Guid, List<TcpSocketApiConnection>>();
        private readonly ConcurrentDictionary<Guid, TcpSocketApiConnection> sessionConnections = new ConcurrentDictionary<Guid, TcpSocketApiConnection>();
        private readonly ConcurrentDictionary<Guid, PendingGameEvents> pendingEvents = new ConcurrentDictionary<Guid, PendingGameEvents>();

        public TcpSocketApiConnection Add(int connectionId, TcpSocketApi server, GameData gameData)
        {
            var connection = new TcpSocketApiConnection(connectionId, server, gameData);
            connections[connectionId] = connection;
            return connection;
        }

        public void AttachSessionToken(int connectionId, SessionToken token, TimeSpan offset)
        {
            if (!connections.TryGetValue(connectionId, out var connection))
                return;

            connection.SessionToken = token;
            connection.TimeOffset = offset;
            connection.Attached = DateTime.UtcNow;
            if (token != null)
            {
                var list = userConnections.GetOrAdd(token.UserId, _ => new List<TcpSocketApiConnection>());
                lock (list)
                {
                    list.Add(connection);
                }
                sessionConnections[token.SessionId] = connection;
            }
        }

        public bool Contains(int connectionId)
        {
            return connections.ContainsKey(connectionId);
        }

        public bool Remove(int connectionId, out TcpSocketApiConnection connection)
        {
            var wasRemoved = connections.TryRemove(connectionId, out connection);
            if (connection == null)
            {
                return false;
            }

            if (connection.SessionToken != null)
            {
                if (sessionConnections.TryGetValue(connection.SessionToken.SessionId, out var sessionConnection)
                    && (sessionConnection.ConnectionId == connectionId || !sessionConnection.Connected))
                {
                    sessionConnections.TryRemove(connection.SessionToken.SessionId, out _);
                }

                if (userConnections.TryGetValue(connection.SessionToken.UserId, out var uCon))
                {
                    lock (uCon)
                    {
                        uCon.RemoveAll(x => !x.Connected || x.ConnectionId == connectionId);
                    }
                }
            }

            return wasRemoved;
        }

        public bool TryGet(int connectionId, out TcpSocketApiConnection connection)
        {
            return connections.TryGetValue(connectionId, out connection);
        }

        public IReadOnlyList<TcpSocketApiConnection> GetAllConnectionsByUserId(Guid userId)
        {
            if (userConnections.TryGetValue(userId, out var list))
            {
                lock (list)
                {
                    return list.ToArray();
                }
            }

            return Array.Empty<TcpSocketApiConnection>();
        }

        public bool TryGet(Guid sessionId, out TcpSocketApiConnection connection)
        {
            if (sessionConnections.TryGetValue(sessionId, out connection))
            {
                if (connection.Connected)
                {
                    return true;
                }

                var userId = connection.SessionToken?.UserId;
                Remove(connection.ConnectionId, out _);
                connection = userId != null ? GetLatestConnection(userId.Value) : null;
                return connection != null;
            }

            connection = null;
            return false;
        }

        /// <summary>The streamer's newest connected, signed-in connection: the one their events go to.</summary>
        public TcpSocketApiConnection GetLatestConnection(Guid userId)
        {
            TcpSocketApiConnection latest = null;
            foreach (var connection in GetAllConnectionsByUserId(userId))
            {
                if (connection.Connected && connection.SessionToken != null && (latest == null || connection.Attached > latest.Attached))
                {
                    latest = connection;
                }
            }
            return latest;
        }

        /// <summary>The events waiting to go to the streamer's client (see <see cref="PendingGameEvents"/>).</summary>
        public PendingGameEvents GetPendingEvents(Guid userId)
        {
            return pendingEvents.GetOrAdd(userId, _ => new PendingGameEvents());
        }
    }
}

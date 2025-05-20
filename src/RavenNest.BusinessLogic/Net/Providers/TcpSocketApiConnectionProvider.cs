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
            if (token != null)
            {
                userConnections.AddOrUpdate(token.UserId,
                    new List<TcpSocketApiConnection> {
                            connection
                    },
                    (key, list) =>
                    {
                        list.Add(connection);
                        return list;
                    });
                sessionConnections[token.SessionId] = connection;
            }
        }

        public bool Contains(int connectionId)
        {
            return connections.ContainsKey(connectionId);
        }

        public bool Remove(int connectionId, out TcpSocketApiConnection connection)
        {
            connections.TryGetValue(connectionId, out connection);
            var wasRemoved = connections.TryRemove(connectionId, out _);

            if (connection.SessionToken != null)
            {
                if (sessionConnections.TryGetValue(connection.SessionToken.SessionId, out var sessionConnection) && !sessionConnection.Connected)
                {
                    sessionConnections.TryRemove(connection.SessionToken.SessionId, out _);
                }

                if (userConnections.TryGetValue(connection.SessionToken.UserId, out var uCon))
                {
                    uCon.RemoveAll(x => !x.Connected || x.ConnectionId == connectionId);
                    userConnections[connection.SessionToken.UserId] = uCon;
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
            userConnections.TryGetValue(userId, out var connections);
            if (connections != null)
                return connections;

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

                if (!connection.Connected)
                {
                    Remove(connection.ConnectionId, out _);
                }
            }

            if (userConnections.TryGetValue(connection.SessionToken.UserId, out var uCon))
            {
                connection = uCon.FirstOrDefault(x => x.Connected);
            }

            return connection != null;
        }

    }
}

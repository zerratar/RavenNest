using System;
using System.Collections.Generic;
using System.Linq;

namespace RavenNest.BusinessLogic.Net
{
    public class TcpSocketApiConnectionProvider : ITcpSocketApiConnectionProvider
    {
        private readonly Dictionary<int, TcpSocketApiConnection> connections = new Dictionary<int, TcpSocketApiConnection>();
        private readonly object syncRoot = new object();

        public void Add(int connectionId, TcpSocketApi server)
        {
            lock (syncRoot)
            {
                connections[connectionId] = new TcpSocketApiConnection(connectionId, server);
            }
        }

        public bool Contains(int connectionId)
        {
            lock (syncRoot)
                return connections.ContainsKey(connectionId);
        }

        public bool Remove(int connectionId)
        {
            lock (syncRoot)
                return connections.Remove(connectionId);
        }

        public bool TryGet(int connectionId, out TcpSocketApiConnection connection)
        {
            lock (syncRoot)
                return connections.TryGetValue(connectionId, out connection);

        }

        public bool TryGet(Guid sessionId, out TcpSocketApiConnection connection)
        {
            lock (syncRoot)
            {
                //connection = connections.Values.OrderByDescending(x => x.Created).FirstOrDefault(x => x.SessionToken != null && x.SessionToken.SessionId == sessionId);
                connection = null;
                List<int> removeConnections = null;
                foreach (var c in connections.Values.OrderByDescending(x => x.Created).Where(x =>
                     x.SessionToken != null &&
                     x.SessionToken.SessionId == sessionId))
                {
                    if (connection == null || !connection.Connected)
                    {
                        connection = c;
                    }

                    if (!c.Connected)
                    {
                        // this is not good, we still have disconnected clients.
                        // add for removal.
                        if (removeConnections == null)
                        {
                            removeConnections = new List<int>();
                        }
                        removeConnections.Add(c.ConnectionId);
                    }
                }

                if (removeConnections != null && removeConnections.Count > 0)
                {
                    foreach (var c in removeConnections)
                    {
                        Remove(c);
                    }
                }

                return connection != null;
            }
        }
    }
}

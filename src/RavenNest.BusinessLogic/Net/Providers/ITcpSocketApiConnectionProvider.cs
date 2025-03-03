using RavenNest.BusinessLogic.Data;
using System;

namespace RavenNest.BusinessLogic.Net
{
    // Used for external resources to access a tcp api connection
    public interface ITcpSocketApiConnectionProvider
    {
        TcpSocketApiConnection Add(int connectionId, TcpSocketApi server, GameData gameData);
        bool TryGet(int connectionId, out TcpSocketApiConnection connection);
        bool TryGet(Guid sessionId, out TcpSocketApiConnection connection);
        bool Contains(int connectionId);
        bool Remove(int connectionId, out TcpSocketApiConnection connection);
    }
}

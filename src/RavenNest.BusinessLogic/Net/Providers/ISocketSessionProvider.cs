using RavenNest.BusinessLogic.Game;
using RavenNest.Models;
using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Threading.Tasks;

namespace RavenNest.BusinessLogic.Net
{
    public interface IGameWebSocketConnectionProvider
    {
        bool TryGet(Guid sessionId, out IGameWebSocketConnection session);
        bool TryGet(SessionToken token, out IGameWebSocketConnection session);
        IGameWebSocketConnection Get(WebSocket ws, IReadOnlyDictionary<string, string> requestHeaders);
        void KillAllConnections();
    }
}

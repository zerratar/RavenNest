using RavenNest.BusinessLogic.Game;
using RavenNest.Models;
using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Threading.Tasks;

namespace RavenNest.BusinessLogic.Net
{
    public interface IWebSocketConnectionProvider
    {
        bool TryGet(Guid sessionId, out IWebSocketConnection session);
        bool TryGet(SessionToken token, out IWebSocketConnection session);
        IWebSocketConnection Get(WebSocket ws, IReadOnlyDictionary<string, string> requestHeaders);
    }
}

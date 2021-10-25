using System.Collections.Generic;

namespace RavenNest.BusinessLogic.Twitch.Extension
{
    public interface IExtensionWebSocketConnectionProvider
    {
        IReadOnlyList<IExtensionConnection> GetAll();
        IExtensionConnection Get(System.Net.WebSockets.WebSocket socket, IReadOnlyDictionary<string, string> requestHeaders);
    }
}

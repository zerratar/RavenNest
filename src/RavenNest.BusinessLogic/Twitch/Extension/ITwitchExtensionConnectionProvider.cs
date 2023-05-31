using System;
using System.Collections.Generic;

namespace RavenNest.BusinessLogic.Twitch.Extension
{
    public interface ITwitchExtensionConnectionProvider
    {
        IEnumerable<IExtensionConnection> GetAll();
        IExtensionConnection Get(System.Net.WebSockets.WebSocket socket, IReadOnlyDictionary<string, string> requestHeaders);
        bool TryGet(string sessionId, out IExtensionConnection connection);
        bool TryGetAllByUser(Guid userId, out IReadOnlyList<IExtensionConnection> connections);
        bool TryGetAllByStreamer(Guid streamerUserId, out IReadOnlyList<IExtensionConnection> connections);
        bool TryGet(Guid characterId, out IExtensionConnection connection);
    }
}

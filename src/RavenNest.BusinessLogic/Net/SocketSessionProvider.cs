using System;
using System.Net.WebSockets;
using System.Threading.Tasks;

namespace RavenNest.BusinessLogic.Net
{
    public class SocketSessionProvider : ISocketSessionProvider
    {
        public Task<ISocketSession> GetAsync(WebSocket ws)
        {
            throw new NotImplementedException();
        }
    }
}

using System.Net.WebSockets;
using System.Threading.Tasks;

namespace RavenNest.BusinessLogic.Net
{
    public interface ISocketSessionProvider
    {
        Task<ISocketSession> GetAsync(WebSocket ws);
    }
}

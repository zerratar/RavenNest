using System.Threading.Tasks;

namespace RavenNest.BusinessLogic.Net
{
    public interface IGamePacketHandler
    {
        Task HandleAsync(IGameWebSocketConnection connection, GamePacket packet);
    }
}
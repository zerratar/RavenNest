using System.Threading.Tasks;
using RavenNest.BusinessLogic.Net;

namespace RavenNest.TestClient
{
    public class GameEventPacketHandler : GamePacketHandler
    {
        public GameEventPacketHandler(IGameManager gameManager)
            : base(gameManager)
        {
        }

        public override Task HandleAsync(GamePacket packet)
        {
            return Task.CompletedTask;
        }
    }
}

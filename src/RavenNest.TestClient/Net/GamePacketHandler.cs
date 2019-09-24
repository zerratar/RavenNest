using System.Threading.Tasks;
using RavenNest.BusinessLogic.Net;

namespace RavenNest.TestClient
{
    public abstract class GamePacketHandler
    {
        protected readonly IGameManager GameManager;

        protected GamePacketHandler(IGameManager gameManager)
        {
            this.GameManager = gameManager;
        }
        public abstract Task HandleAsync(GamePacket packet);
    }
}

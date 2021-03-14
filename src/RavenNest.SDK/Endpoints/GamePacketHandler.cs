using System.Threading.Tasks;

namespace RavenNest.SDK.Endpoints
{
    public abstract class GamePacketHandler
    {
        protected readonly IGameManager GameManager;

        protected GamePacketHandler(IGameManager gameManager)
        {
            GameManager = gameManager;
        }
        public abstract Task HandleAsync(GamePacket packet);
    }
}

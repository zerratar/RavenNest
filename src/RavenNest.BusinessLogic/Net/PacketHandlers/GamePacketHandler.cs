using RavenNest.BusinessLogic.Game;
using System.Threading.Tasks;

namespace RavenNest.BusinessLogic.Net
{
    public abstract class GamePacketHandler
    {
        private readonly GameManager gameManager;

        protected GamePacketHandler(GameManager gameManager)
        {
            this.gameManager = gameManager;
        }

        public abstract void Handle(GamePacket packet);
    }
}

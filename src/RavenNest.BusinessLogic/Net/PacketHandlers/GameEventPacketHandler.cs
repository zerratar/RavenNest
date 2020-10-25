using RavenNest.BusinessLogic.Game;

namespace RavenNest.BusinessLogic.Net
{
    public class GameEventPacketHandler : GamePacketHandler
    {
        public GameEventPacketHandler(GameManager gameManager)
            : base(gameManager)
        {
        }

        public override void Handle(GamePacket packet)
        {
        }
    }
}

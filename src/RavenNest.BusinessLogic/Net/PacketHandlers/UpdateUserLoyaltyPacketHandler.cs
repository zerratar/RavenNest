using RavenNest.BusinessLogic.Game;
using System.Threading.Tasks;

namespace RavenNest.BusinessLogic.Net
{
    internal class UpdateUserLoyaltyPacketHandler : IGamePacketHandler
    {
        private readonly PlayerManager playerManager;

        public UpdateUserLoyaltyPacketHandler(PlayerManager playerManager)
        {
            this.playerManager = playerManager;
        }

        public Task HandleAsync(IGameWebSocketConnection connection, GamePacket packet)
        {
            if (packet.Data is UserLoyaltyUpdate update)
            {
                this.playerManager.UpdateUserLoyalty(connection.SessionToken, update);
            }

            return Task.CompletedTask;
        }
    }
}

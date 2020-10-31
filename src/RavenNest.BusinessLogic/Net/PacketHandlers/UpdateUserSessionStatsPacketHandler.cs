using RavenNest.BusinessLogic.Game;
using System.Threading;
using System.Threading.Tasks;

namespace RavenNest.BusinessLogic.Net
{
    internal class UpdateUserSessionStatsPacketHandler : IGamePacketHandler
    {
        private readonly IPlayerManager playerManager;

        public UpdateUserSessionStatsPacketHandler(IPlayerManager playerManager)
        {
            this.playerManager = playerManager;
        }
        public async Task HandleAsync(IWebSocketConnection connection, GamePacket packet)
        {
            var result = false;

            if (packet.Data is PlayerSessionActivity update)
            {
                this.playerManager.UpdatePlayerActivity(connection.SessionToken, update);
            }

            //await connection.ReplyAsync(packet.CorrelationId, packet.Type, result, CancellationToken.None);
        }
    }
}

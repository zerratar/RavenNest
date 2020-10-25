using RavenNest.BusinessLogic.Game;
using System.Threading;
using System.Threading.Tasks;

namespace RavenNest.BusinessLogic.Net
{
    internal class UpdateGameStatsPacketHandler : IGamePacketHandler
    {
        private readonly IPlayerManager playerManager;

        public UpdateGameStatsPacketHandler(IPlayerManager playerManager)
        {
            this.playerManager = playerManager;
        }

        public async Task HandleAsync(IWebSocketConnection connection, GamePacket packet)
        {
            var result = false;

            await connection.ReplyAsync(packet.CorrelationId, packet.Type, result, CancellationToken.None);
        }
    }
}

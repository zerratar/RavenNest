using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Game;
using System.Threading.Tasks;

namespace RavenNest.BusinessLogic.Net
{
    internal class UpdateCharacterStatePacketHandler : IGamePacketHandler
    {
        private readonly ILogger logger;
        private readonly IPlayerManager playerManager;

        public UpdateCharacterStatePacketHandler(ILogger logger, IPlayerManager playerManager)
        {
            this.logger = logger;
            this.playerManager = playerManager;
        }

        public async Task HandleAsync(IGameWebSocketConnection connection, GamePacket packet)
        {
            var result = false;

            if (packet.Data is CharacterStateUpdate update)
            {
                result = this.playerManager.UpdatePlayerState(connection.SessionToken, update);
            }
            else
            {
                logger.LogError("CharacterStateUpdate package received but the packet data did not contain the proper structure.");
            }

            //await connection.ReplyAsync(packet, result);
        }
    }
}

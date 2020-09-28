using RavenNest.BusinessLogic.Game;
using System.Threading;
using System.Threading.Tasks;

namespace RavenNest.BusinessLogic.Net
{
    internal class UpdateCharacterStatePacketHandler : IGamePacketHandler
    {
        private readonly IPlayerManager playerManager;

        public UpdateCharacterStatePacketHandler(IPlayerManager playerManager)
        {
            this.playerManager = playerManager;
        }
        public async Task HandleAsync(
            IWebSocketConnection connection,
            GamePacket packet)
        {
            var result = false;

            if (packet.Data is CharacterStateUpdate update)
            {
                result = this.playerManager.UpdatePlayerState(connection.SessionToken, update);
            }

            await connection.ReplyAsync(packet.CorrelationId, packet.Type, result, CancellationToken.None);
        }
    }

    internal class UpdateCharacterSkillPacketHandler : IGamePacketHandler
    {
        private readonly IPlayerManager playerManager;

        public UpdateCharacterSkillPacketHandler(IPlayerManager playerManager)
        {
            this.playerManager = playerManager;
        }
        public async Task HandleAsync(
            IWebSocketConnection connection,
            GamePacket packet)
        {
            var result = false;

            if (packet.Data is CharacterSkillUpdate update)
            {
                result = this.playerManager.UpdateExperience(
                    connection.SessionToken, update.UserId, update.Level, update.Experience, update.CharacterId);
            }

            await connection.ReplyAsync(packet.CorrelationId, packet.Type, result, CancellationToken.None);
        }

    }
}

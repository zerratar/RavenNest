using RavenNest.BusinessLogic.Game;
using System.Threading;
using System.Threading.Tasks;

namespace RavenNest.BusinessLogic.Net
{
    internal class UpdateCharacterStatisticsPacketHandler : IGamePacketHandler
    {
        private readonly IPlayerManager playerManager;

        public UpdateCharacterStatisticsPacketHandler(IPlayerManager playerManager)
        {
            this.playerManager = playerManager;
        }

        public async Task HandleAsync(IGameWebSocketConnection connection, GamePacket packet)
        {
            var result = false;

            //if (packet.Data is CharacterSkillUpdate update)
            //{
            //    result = this.playerManager.UpdateExperience(
            //        connection.SessionToken, update.UserId, update.Level, update.Experience, update.CharacterId);
            //}

            await connection.ReplyAsync(packet.CorrelationId, packet.Type, result, CancellationToken.None);
        }
    }
}

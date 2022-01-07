using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Game;
using System.Threading.Tasks;

namespace RavenNest.BusinessLogic.Net
{
    internal class UpdateCharacterExperiencePacketHandler : IGamePacketHandler
    {
        private readonly ILogger logger;
        private readonly IPlayerManager playerManager;

        public UpdateCharacterExperiencePacketHandler(ILogger logger, IPlayerManager playerManager)
        {
            this.logger = logger;
            this.playerManager = playerManager;
        }
        public async Task HandleAsync(IGameWebSocketConnection connection, GamePacket packet)
        {
            var result = false;

            if (packet.TryGetValue<CharacterExpUpdate>(out var update))
            {
                result = this.playerManager.UpdateExperience(
                    connection.SessionToken,
                    update.SkillIndex,
                    update.Level,
                    update.Experience,
                    update.CharacterId);
            }
            else
            {
                logger.LogError("CharacterExpUpdate package received but the packet data did not contain the proper structure.");
            }

            //await connection.ReplyAsync(packet.CorrelationId, packet.Type, result, CancellationToken.None);
        }
    }
}

using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Game;
using System.Threading.Tasks;

namespace RavenNest.BusinessLogic.Net
{
    internal class UpdateCharacterExperiencePacketHandler : IGamePacketHandler
    {
        private readonly ILogger logger;
        private readonly PlayerManager playerManager;

        //private ConcurrentDictionary<>

        public UpdateCharacterExperiencePacketHandler(ILogger logger, PlayerManager playerManager)
        {
            this.logger = logger;
            this.playerManager = playerManager;
        }
        public async Task HandleAsync(IGameWebSocketConnection connection, GamePacket packet)
        {
            var result = false;

            if (connection == null)
            {
                logger.LogError("Connection dropped during saving.");
                return;
            }
            if (connection.SessionToken == null && packet != null)
            {
                logger.LogError("SessionToken is null. UpdateCharacterExperiencePacketHandler:" + packet.Data);
                return;
            }

            if (packet.TryGetValue<CharacterExpUpdate>(out var update))
            {
                if (connection.SessionToken.Expired)
                {
                    logger.LogError("Trying to saving character but session expired. " + connection.SessionToken.SessionId);
                    return;
                }

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

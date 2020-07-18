using RavenNest.BusinessLogic.Game;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace RavenNest.BusinessLogic.Net
{
    public class GamePacketManager : IGamePacketManager
    {
        private readonly ConcurrentDictionary<string, IGamePacketHandler> packetHandlers
            = new ConcurrentDictionary<string, IGamePacketHandler>();
        private readonly ILogger logger;
        private readonly IPlayerManager playerManager;

        public GamePacketManager(ILogger<GamePacketManager> logger, IPlayerManager playerManager)
        {
            this.logger = logger;
            this.playerManager = playerManager;
            Default = new UnsupportedPacketHandler(logger);
            packetHandlers["update_character_state"] = new UpdateCharacterStatePacketHandler(playerManager);
        }

        public IGamePacketHandler Default { get; }

        public bool TryGetPacketHandler(string id, out IGamePacketHandler packetHandler)
        {
            return packetHandlers.TryGetValue(id, out packetHandler);
        }
    }
}
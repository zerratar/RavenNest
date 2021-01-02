using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Providers;

namespace RavenNest.BusinessLogic.Game
{
    public class LoyaltyManager : ILoyaltyManager
    {
        private readonly ILogger logger;
        private readonly IPlayerInventoryProvider inventoryProvider;
        private readonly IGameData gameData;

        public LoyaltyManager(
            ILogger<LoyaltyManager> logger,
            IPlayerInventoryProvider inventoryProvider,
            IGameData gameData)
        {
            this.logger = logger;
            this.inventoryProvider = inventoryProvider;
            this.gameData = gameData;
        }
    }
}

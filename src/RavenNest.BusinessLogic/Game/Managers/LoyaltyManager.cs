using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Providers;

namespace RavenNest.BusinessLogic.Game
{
    public class LoyaltyManager : ILoyaltyManager
    {
        private readonly ILogger logger;
        private readonly PlayerInventoryProvider inventoryProvider;
        private readonly GameData gameData;

        public LoyaltyManager(
            ILogger<LoyaltyManager> logger,
            PlayerInventoryProvider inventoryProvider,
            GameData gameData)
        {
            this.logger = logger;
            this.inventoryProvider = inventoryProvider;
            this.gameData = gameData;
        }
    }
}

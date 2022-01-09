using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Game;
using RavenNest.Models;
using System.Threading.Tasks;

namespace RavenNest.Blazor.Services
{
    public class MarketplaceService
    {
        private readonly IGameData gameData;
        private readonly IMarketplaceManager marketplaceManager;

        public MarketplaceService(
            IGameData gameData,
            IMarketplaceManager marketplaceManager)
        {
            this.gameData = gameData;
            this.marketplaceManager = marketplaceManager;
        }
        public async Task<MarketItemCollection> GetMarketItemsAsync()
        {
            return await Task.Run(() =>
            {
                return marketplaceManager.GetItems(0, int.MaxValue);
            });
        }

        public async Task<bool> CancelListingAsync(System.Guid id)
        {
            return await Task.Run(() =>
            {
                return marketplaceManager.Cancel(id);
            });
        }
    }
}

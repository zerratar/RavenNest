using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Game;
using RavenNest.Models;
using System.Threading.Tasks;

namespace RavenNest.Blazor.Services
{
    public class MarketplaceService
    {
        private readonly GameData gameData;
        private readonly MarketplaceManager marketplaceManager;

        public MarketplaceService(
            GameData gameData,
            MarketplaceManager marketplaceManager)
        {
            this.gameData = gameData;
            this.marketplaceManager = marketplaceManager;
        }



        public async Task<MarketItemCollection> GetMarketItemsAsync(ItemFilter filter)
        {
            return await Task.Run(() =>
            {
                return marketplaceManager.GetItems(filter, 0, int.MaxValue);
            });
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

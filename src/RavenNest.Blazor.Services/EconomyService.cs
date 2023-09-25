using Microsoft.AspNetCore.Http;
using RavenNest.BusinessLogic.Data;
using RavenNest.DataModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RavenNest.Blazor.Services
{
    public class EconomyService : RavenNestService
    {
        private readonly GameData gameData;

        public EconomyService(GameData gameData,
            IHttpContextAccessor accessor,
            SessionInfoProvider sessionInfoProvider)
            : base(accessor, sessionInfoProvider)
        {
            this.gameData = gameData;
        }

        public IReadOnlyList<DailyAggregatedMarketplaceData> GetMarketplaceReports(DateTime startDate, DateTime endDate)
        {
            return gameData.GetMarketplaceReports(startDate, endDate);
        }

        public IReadOnlyList<DailyAggregatedMarketplaceData> GetMarketplaceReports()
        {
            return gameData.GetMarketplaceReports(DateTime.UnixEpoch, DateTime.UtcNow);
        }

        public IReadOnlyList<MarketItemTransaction> GetMarketItemTransactions(DateTime startDate, DateTime endDate)
        {
            // Assuming you have a method in GameData class to fetch transactions by date range
            return gameData.GetMarketItemTransactions(startDate, endDate);
        }

        public async Task<IReadOnlyList<User>> GetTopRichestPlayers(int count)
        {
            return await Task.Run(() =>
            {
                var characters = gameData.GetUsers();
                return characters.OrderByDescending(c => gameData.GetResources(c)?.Coins ?? 0)
                                 .Take(count)
                                 .ToList();
            });
        }
        // Add any other methods needed to fetch or manipulate economy data
    }
}

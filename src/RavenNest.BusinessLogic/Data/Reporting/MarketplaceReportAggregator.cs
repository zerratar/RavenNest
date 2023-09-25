using RavenNest.DataModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RavenNest.BusinessLogic.Data.Aggregators
{

    public class MarketplaceReportAggregator : DataAggregator
    {
        public MarketplaceReportAggregator(GameData gameData)
            : base(gameData, TimeSpan.FromHours(2), TimeSpan.FromDays(30), TimeSpan.FromDays(1))
        {
        }
        protected override void AggregateReport()
        {
            RemoveOldReports();

            var marketplaceItems = gameData.GetMarketItems();

            var groupedItems = marketplaceItems.GroupBy(x => x.ItemId)
                .Select(g => new
                {
                    ItemId = g.Key,
                    AvgPrice = g.Average(x => x.PricePerItem),
                    Amount = g.Sum(x => x.Amount),
                    Sellers = g.Select(x => x.SellerCharacterId).Distinct().Count()
                });

            var now = DateTime.UtcNow.Date;

            foreach (var item in groupedItems)
            {
                var data = new DailyAggregatedMarketplaceData
                {
                    Id = Guid.NewGuid(),
                    Date = now,
                    ItemId = item.ItemId,
                    AvgPrice = item.AvgPrice,
                    Amount = item.Amount,
                    Sellers = item.Sellers
                };

                gameData.Add(data);
            }
        }

        protected override void RemoveOldReports()
        {
            var currentDate = DateTime.UtcNow.Date;
            var retentionDate = currentDate - retentionTime;
            var oldReports = gameData.GetMarketplaceReports(DateTime.UnixEpoch, retentionDate);
            foreach (var oldReport in oldReports)
            {
                gameData.Remove(oldReport);
            }
        }
    }
}

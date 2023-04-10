using RavenNest.DataModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RavenNest.BusinessLogic.Data
{
    public class MarketplaceReportAggregator : IDisposable
    {
        private readonly GameData gameData;
        private readonly Timer timer;
        private readonly TimeSpan aggregationTime = TimeSpan.FromHours(2);
        private readonly TimeSpan retentionTime = TimeSpan.FromDays(30);
        private bool disposed;

        public MarketplaceReportAggregator(GameData gameData)
        {
            this.gameData = gameData;

            // Calculate the time until the next aggregation
            var nextAggregation = DateTime.UtcNow.Date.Add(aggregationTime);
            if (nextAggregation <= DateTime.UtcNow)
            {
                nextAggregation = nextAggregation.AddDays(1);
            }

            var interval = nextAggregation - DateTime.UtcNow;
            timer = new Timer(OnTimerTick, null, interval, TimeSpan.FromDays(1));
        }

        private void OnTimerTick(object state)
        {
            Task.Run(() => AggregateReport());
        }

        private void AggregateReport()
        {
            RemoveOldReports();

            var marketplaceItems = gameData.GetMarketItems();

            // Group by ItemId and aggregate the data for each group
            var groupedItems = marketplaceItems.GroupBy(x => x.ItemId)
                .Select(g => new
                {
                    ItemId = g.Key,
                    AvgPrice = g.Average(x => x.PricePerItem),
                    Amount = g.Sum(x => x.Amount),
                    Sellers = g.Select(x => x.SellerCharacterId).Distinct().Count()
                });

            var now = DateTime.UtcNow.Date;

            // Add the aggregated data to the database
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

        private void RemoveOldReports()
        {
            var currentDate = DateTime.UtcNow.Date;
            var retentionDate = currentDate - retentionTime;
            var oldReports = gameData.GetMarketplaceReports(DateTime.MinValue, retentionDate);
            foreach (var oldReport in oldReports)
            {
                gameData.Remove(oldReport);
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    // Dispose managed resources
                    timer.Dispose();
                }

                disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}

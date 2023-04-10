using System;

namespace RavenNest.DataModels
{
    public partial class DailyAggregatedMarketplaceData : Entity<DailyAggregatedMarketplaceData>
    {
        private DateTime date;
        public DateTime Date
        {
            get => date;
            set => Set(ref date, value);
        }

        private Guid itemId;
        public Guid ItemId
        {
            get => itemId;
            set => Set(ref itemId, value);
        }

        private double avgPrice;
        public double AvgPrice
        {
            get => avgPrice;
            set => Set(ref avgPrice, value);
        }

        private long amount;
        public long Amount
        {
            get => amount;
            set => Set(ref amount, value);
        }

        private long sellers;
        public long Sellers
        {
            get => sellers;
            set => Set(ref sellers, value);
        }
    }
}

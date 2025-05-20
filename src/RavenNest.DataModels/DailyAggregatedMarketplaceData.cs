using RavenNest.DataAnnotations;
using System;

namespace RavenNest.DataModels
{
    public partial class DailyAggregatedMarketplaceData : Entity<DailyAggregatedMarketplaceData>
    {
        [PersistentData] private DateTime date;
        [PersistentData] private Guid itemId;
        [PersistentData] private double avgPrice;
        [PersistentData] private long amount;
        [PersistentData] private long sellers;
    }
}

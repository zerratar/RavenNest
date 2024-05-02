namespace RavenNest.Models
{
    public class ItemValueResult
    {
        public ItemValueResult() { }
        public ItemValueResult(
            double minPrice,
            double maxPrice,
            double avgPrice,
            long availableAmount,
            double costForAmount)
        {
            MinPrice = minPrice;
            MaxPrice = maxPrice;
            AvgPrice = avgPrice;
            AvailableAmount = availableAmount;
            CostForAmount = costForAmount;
        }

        public double MinPrice { get; set; }
        public double MaxPrice { get; set; }
        public double AvgPrice { get; set; }
        public long AvailableAmount { get; set; }
        public double CostForAmount { get; set; }
    }
}

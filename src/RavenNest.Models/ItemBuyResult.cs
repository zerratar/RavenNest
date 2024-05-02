namespace RavenNest.Models
{
    public class ItemBuyResult
    {
        public ItemBuyResult() { }
        public ItemBuyResult(
            ItemTradeState state,
            long[] amountBought,
            double[] costPerItem,
            long totalAmount,
            double totalCost)
        {
            State = state;
            AmountBought = amountBought;
            CostPerItem = costPerItem;
            TotalAmount = totalAmount;
            TotalCost = totalCost;
        }

        public ItemTradeState State { get; set; }
        public long[] AmountBought { get; set; }
        public double[] CostPerItem { get; set; }
        public long TotalAmount { get; set; }
        public double TotalCost { get; set; }

    }
}

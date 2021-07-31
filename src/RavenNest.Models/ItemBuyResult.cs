namespace RavenNest.Models
{
    public class ItemBuyResult
    {
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

        public ItemTradeState State { get; }
        public long[] AmountBought { get; }
        public double[] CostPerItem { get; }
        public long TotalAmount { get; }
        public double TotalCost { get; }

    }

}

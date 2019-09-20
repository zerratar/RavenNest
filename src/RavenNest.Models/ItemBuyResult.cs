namespace RavenNest.Models
{
    public class ItemBuyResult
    {
        public ItemBuyResult(
            ItemTradeState state, 
            long[] amountBought, 
            decimal[] costPerItem, 
            long totalAmount, 
            decimal totalCost)
        {
            State = state;
            AmountBought = amountBought;
            CostPerItem = costPerItem;
            TotalAmount = totalAmount;
            TotalCost = totalCost;
        }

        public ItemTradeState State { get; }
        public long[] AmountBought { get; }
        public decimal[] CostPerItem { get; }
        public long TotalAmount { get; }
        public decimal TotalCost { get; }

    }

}
namespace RavenNest.Models
{
    public class ItemSellResult
    {
        public ItemSellResult(ItemTradeState state)
        {
            this.State = state;
        }

        public ItemTradeState State { get; }
    }
}
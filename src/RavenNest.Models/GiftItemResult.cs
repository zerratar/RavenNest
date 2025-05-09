namespace RavenNest.Models
{
    public class GiftItemResult
    {
        public static GiftItemResult Error { get; } = new GiftItemResult { Status = GiftItemStatus.ErrUnknown };
        public static GiftItemResult InventoryError { get; } = new GiftItemResult { Status = GiftItemStatus.ErrInventoryLock };
        public static GiftItemResult SoulboundItem { get; } = new GiftItemResult { Status = GiftItemStatus.ErrSoulboundItem };
        public static GiftItemResult NoItem { get; } = new GiftItemResult { Status = GiftItemStatus.ErrNoItem };
        public InventoryItem StackToIncrement { get; set; }
        public InventoryItem StackToDecrement { get; set; }
        public long Amount { get; set; }
        public GiftItemStatus Status { get; set; }

        public static GiftItemResult OK(long amount, InventoryItem stackToIncrement, InventoryItem stackToDecrement)
        {
            return new GiftItemResult
            {
                Status = GiftItemStatus.OK,
                Amount = amount,
                StackToIncrement = stackToIncrement,
                StackToDecrement = stackToDecrement
            };
        }
    }
}

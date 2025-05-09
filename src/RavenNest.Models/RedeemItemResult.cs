using System;

namespace RavenNest.Models
{
    public class RedeemItemResult
    {
        public RedeemItemResultCode Code { get; set; }
        public Guid RedeemedItemId { get; set; }
        public Guid InventoryItemId { get; set; }
        public int RedeemedItemAmount { get; set; }
        public Guid CurrencyItemId { get; set; }
        public long CurrencyLeft { get; set; }
        public int CurrencyCost { get; set; }
        public string ErrorMessage { get; set; }
        public static RedeemItemResult Ok()
        {
            return new RedeemItemResult
            {
                Code = RedeemItemResultCode.Success
            };
        }
        public static RedeemItemResult NoSuchItem(string itemName = null)
        {
            return new RedeemItemResult
            {
                Code = RedeemItemResultCode.NoSuchItem,
                //ErrorMessage = itemName != null ? itemName + " is not a redeemable item."
            };
        }
        public static RedeemItemResult Error(string message)
        {
            return new RedeemItemResult
            {
                Code = RedeemItemResultCode.InsufficientCurrency,
                ErrorMessage = message
            };
        }
        public static RedeemItemResult InsufficientCurrency(long actualAmount, int expectedAmount, string currency)
        {
            return new RedeemItemResult
            {
                Code = RedeemItemResultCode.InsufficientCurrency,
                ErrorMessage = "You don't have enough " + currency + " to redeem this item; You need " + (expectedAmount - actualAmount) + " more."
            };
        }
    }
}

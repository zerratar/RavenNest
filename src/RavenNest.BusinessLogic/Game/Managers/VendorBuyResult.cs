namespace RavenNest.BusinessLogic.Game
{
    /// <summary>
    ///     What happened when a character tried to buy from the vendor.
    /// </summary>
    /// <remarks>
    ///     A bool would not do here. Every refusal has a different cause the buyer can act on, and
    ///     the amount can legitimately come back smaller than was asked for when the stock ran out
    ///     part way through, which is a success rather than a failure and has to say so.
    /// </remarks>
    public sealed class VendorBuyResult
    {
        public bool Success { get; private set; }
        public string Message { get; private set; }

        /// <summary>How many were actually bought, which may be fewer than requested.</summary>
        public long Amount { get; private set; }

        public long TotalPrice { get; private set; }

        public static VendorBuyResult Failed(string message) =>
            new VendorBuyResult { Success = false, Message = message };

        public static VendorBuyResult Ok(long amount, long totalPrice) =>
            new VendorBuyResult
            {
                Success = true,
                Amount = amount,
                TotalPrice = totalPrice,
                Message = "Bought " + amount.ToString("N0") + " for " + totalPrice.ToString("N0") + " coins."
            };
    }
}

using System;

namespace RavenNest.Models
{
    public class RedeemableItem
    {
        public Guid Id { get; set; }
        public Guid ItemId { get; set; }
        public Guid CurrencyItemId { get; set; }
        public int Cost { get; set; }
        public int Amount { get; set; }
        public string AvailableDateRange { get; set; }
    }
}

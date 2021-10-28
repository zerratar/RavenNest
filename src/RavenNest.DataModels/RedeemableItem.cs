using System;

namespace RavenNest.DataModels
{
    public class RedeemableItem : Entity<RedeemableItem>
    {
        private Guid id; public Guid Id { get => id; set => Set(ref id, value); }
        private Guid itemId; public Guid ItemId { get => itemId; set => Set(ref itemId, value); }
        private Guid currencyItemId; public Guid CurrencyItemId { get => currencyItemId; set => Set(ref currencyItemId, value); }
        private int cost; public int Cost { get => cost; set => Set(ref cost, value); }
        private int amount; public int Amount { get => amount; set => Set(ref amount, value); }
        private string availableDateRange; public string AvailableDateRange { get => availableDateRange; set => Set(ref availableDateRange, value); }
    }
}

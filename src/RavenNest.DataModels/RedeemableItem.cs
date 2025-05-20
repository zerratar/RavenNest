using RavenNest.DataAnnotations;
using System;

namespace RavenNest.DataModels
{
    public partial class RedeemableItem : Entity<RedeemableItem>
    {
        [PersistentData] private Guid itemId;
        [PersistentData] private Guid currencyItemId;
        [PersistentData] private int cost;
        [PersistentData] private int amount;
        [PersistentData] private string availableDateRange;
    }
}

using System;

namespace RavenNest.DataModels
{
    /// <summary>
    ///     The type of effect that will be applied when using an item
    /// </summary>
    public class ItemStatusEffect : Entity<ItemStatusEffect>
    {
        private double amount;
        private double minAmount;
        private double duration;
        private Island? island;

        private Guid itemId;
        private StatusEffectType type;

        public Guid ItemId { get => itemId; set => Set(ref itemId, value); }
        public StatusEffectType Type { get => type; set => Set(ref type, value); }
        public double Amount { get => amount; set => Set(ref amount, value); }
        /// <summary>
        ///     The minimum amount that should be applied, this is to make sure that the Amount % not yielding too low of an effect
        /// </summary>
        public double MinAmount { get => minAmount; set => Set(ref minAmount, value); }
        public double Duration { get => duration; set => Set(ref duration, value); }
        public Island? Island { get => island; set => Set(ref island, value); }
    }
}

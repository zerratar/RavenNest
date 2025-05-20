using RavenNest.DataAnnotations;
using System;

namespace RavenNest.DataModels
{
    /// <summary>
    ///     The type of effect that will be applied when using an item
    /// </summary>
    public partial class ItemStatusEffect : Entity<ItemStatusEffect>
    {
        [PersistentData] private double amount;
        [PersistentData] private double minAmount;
        [PersistentData] private double duration;
        [PersistentData] private Island? island;

        [PersistentData] private Guid itemId;
        [PersistentData] private StatusEffectType type;
    }
}

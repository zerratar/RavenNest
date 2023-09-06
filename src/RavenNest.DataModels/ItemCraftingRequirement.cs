using System;

namespace RavenNest.DataModels
{
    [Obsolete("Use ItemRecipe and ItemRecipeIngredient instead.")]
    public partial class ItemCraftingRequirement : Entity<ItemCraftingRequirement>
    {
        private Guid _ItemId; public Guid ItemId { get => _ItemId; set => Set(ref _ItemId, value); }
        private Guid _ResourceItemId; public Guid ResourceItemId { get => _ResourceItemId; set => Set(ref _ResourceItemId, value); }
        private int _Amount; public int Amount { get => _Amount; set => Set(ref _Amount, value); }
    }
}

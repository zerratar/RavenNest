using RavenNest.DataAnnotations;
using System;

namespace RavenNest.DataModels
{
    [Obsolete("Use ItemRecipe and ItemRecipeIngredient instead.")]
    public partial class ItemCraftingRequirement : Entity<ItemCraftingRequirement>
    {
        [PersistentData] private Guid itemId;
        [PersistentData] private Guid resourceItemId;
        [PersistentData] private int amount;
    }
}

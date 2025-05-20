using RavenNest.DataAnnotations;
using System;

namespace RavenNest.DataModels
{
    public partial class ItemRecipeIngredient : Entity<ItemRecipeIngredient>
    {
        [PersistentData] private Guid recipeId;
        [PersistentData] private Guid itemId;
        [PersistentData] private int amount;
    }
}

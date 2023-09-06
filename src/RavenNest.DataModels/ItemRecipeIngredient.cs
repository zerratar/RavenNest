using System;

namespace RavenNest.DataModels
{
    public class ItemRecipeIngredient : Entity<ItemRecipeIngredient>
    {
        private Guid recipeId;
        private Guid itemId;
        private int amount;

        public Guid RecipeId { get => recipeId; set => Set(ref recipeId, value); }
        public Guid ItemId { get => itemId; set => Set(ref itemId, value); }
        public int Amount { get => amount; set => Set(ref amount, value); }
    }
}

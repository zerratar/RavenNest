using System;

namespace RavenNest.DataModels
{
    public partial class ItemCraftingRequirement : Entity<ItemCraftingRequirement>
    {
        private Guid _ItemId; public Guid ItemId { get => _ItemId; set => Set(ref _ItemId, value); }
        private Guid _ResourceItemId; public Guid ResourceItemId { get => _ResourceItemId; set => Set(ref _ResourceItemId, value); }
        private int _Amount; public int Amount { get => _Amount; set => Set(ref _Amount, value); }
    }

    public class ItemRecipe
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }

        public Guid ItemId { get; set; }
        public Guid? FailedItemId { get; set; }
        public double SuccessRate { get; set; }
        /// <summary>
        /// Whether or not the success rate is fixed or if it should be calculated based on the players skill level.
        /// </summary>
        public bool FixedSuccessRate { get; set; }
        public int RequiredLevel { get; set; }
        public int RequiredSkill { get; set; }
    }

    public class ItemRecipeIngredient
    {
        public Guid Id { get; set; }
        public Guid RecipeId { get; set; }
        public Guid ItemId { get; set; }
        public int Amount { get; set; }
    }
}

using System;

namespace RavenNest.DataModels
{
    public partial class ItemCraftingRequirement : Entity<ItemCraftingRequirement>
    {
        private Guid _ItemId; public Guid ItemId { get => _ItemId; set => Set(ref _ItemId, value); }
        private Guid _ResourceItemId; public Guid ResourceItemId { get => _ResourceItemId; set => Set(ref _ResourceItemId, value); }
        private int _Amount; public int Amount { get => _Amount; set => Set(ref _Amount, value); }
    }

    public class ItemRecipe : Entity<ItemRecipe>
    {
        private string name;
        private string description;
        private Guid itemId;
        private Guid? failedItemId;
        private double successRate;
        private double preparationTime;
        private bool fixedSuccessRate;
        private int requiredLevel;
        private int requiredSkill;

        public string Name { get => name; set => Set(ref name, value); }
        public string Description { get => description; set => Set(ref description, value); }
        public Guid ItemId { get => itemId; set => Set(ref itemId, value); }
        public Guid? FailedItemId { get => failedItemId; set => Set(ref failedItemId, value); }
        public double MaxSuccessRate { get => successRate; set => Set(ref successRate, value); }
        public double MinSuccessRate { get => successRate; set => Set(ref successRate, value); }

        /// <summary>
        ///     How long in seconds will it take for this to be created (per item)
        /// </summary>
        public double PreparationTime { get => preparationTime; set => Set(ref preparationTime, value); }
        /// <summary>
        /// Whether or not the success rate is fixed or if it should be calculated based on the players skill level.
        /// </summary>
        public bool FixedSuccessRate { get => fixedSuccessRate; set => Set(ref fixedSuccessRate, value); }
        public int RequiredLevel { get => requiredLevel; set => Set(ref requiredLevel, value); }
        public int RequiredSkill { get => requiredSkill; set => Set(ref requiredSkill, value); }
    }

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

using System;
using System.Collections.Generic;

namespace RavenNest.Models
{
    public class ItemCraftingRequirement
    {
        public Guid Id { get; set; }
        public Guid ItemId { get; set; }
        public Guid ResourceItemId { get; set; }
        public int Amount { get; set; }
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

        public List<ItemRecipeIngredient> Ingredients { get; set; }
    }

    public class ItemRecipeIngredient
    {
        public Guid ItemId { get; set; }
        public int Amount { get; set; }
    }
}

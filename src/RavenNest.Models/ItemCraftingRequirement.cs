using System;
using System.Collections.Generic;

namespace RavenNest.Models
{
    public class EventItemReward
    {
        public Guid CharacterId { get; set; }
        public Guid ItemId { get; set; }
        public int Amount { get; set; }
    }

    public class ItemCraftingRequirement
    {
        public Guid Id { get; set; }
        public Guid ItemId { get; set; }
        public Guid ResourceItemId { get; set; }
        public int Amount { get; set; }
    }

    public class ItemProductionResult
    {
        public bool Success { get; set; }

        public List<ItemProductionResultItem> Items { get; set; }
    }

    public class ItemProductionResultItem
    {
        /// <summary>
        ///     Whether the creation of these items were due to a successful production or not. If this value is false, this is a bi-product.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Item Id of the item that was created.
        /// </summary>
        public Guid ItemId { get; set; }

        /// <summary>
        ///     Use this to merge existing stack or create a new stack with this.
        /// </summary>
        public Guid InventoryItemId { get; set; }

        /// <summary>
        ///     The new stack amount after the merge or creation.
        /// </summary>
        public long StackAmount { get; set; }

        /// <summary>
        ///     The amount that was successfully created.
        /// </summary>
        public long Amount { get; set; }
    }

    public class ItemRecipe
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public Guid ItemId { get; set; }
        public Guid? FailedItemId { get; set; }
        public double MinSuccessRate { get; set; }
        public double MaxSuccessRate { get; set; }
        /// <summary>
        ///     How long in seconds will it take for this to be created (per item)
        /// </summary>
        public double PreparationTime { get; set; }
        /// <summary>
        /// Whether or not the success rate is fixed or if it should be calculated based on the players skill level.
        /// </summary>
        public bool FixedSuccessRate { get; set; }
        public int RequiredLevel { get; set; }
        public Skill RequiredSkill { get; set; }
        public List<ItemRecipeIngredient> Ingredients { get; set; }
    }

    public class ItemRecipeIngredient
    {
        public Guid ItemId { get; set; }
        public int Amount { get; set; }
    }
}

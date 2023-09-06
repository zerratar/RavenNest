using System;

namespace RavenNest.DataModels
{
    public class ItemRecipe : Entity<ItemRecipe>
    {
        private string name;
        private string description;
        private Guid itemId;
        private Guid? failedItemId;
        private double minSuccessRate;
        private double maxSuccessRate;
        private double preparationTime;
        private bool fixedSuccessRate;
        private int requiredLevel;
        private int requiredSkill;
        public int amount;

        public string Name { get => name; set => Set(ref name, value); }
        public string Description { get => description; set => Set(ref description, value); }
        public int Amount { get => amount; set => Set(ref amount, value); }
        public Guid ItemId { get => itemId; set => Set(ref itemId, value); }
        public Guid? FailedItemId { get => failedItemId; set => Set(ref failedItemId, value); }
        public double MaxSuccessRate { get => maxSuccessRate; set => Set(ref maxSuccessRate, value); }
        public double MinSuccessRate { get => minSuccessRate; set => Set(ref minSuccessRate, value); }

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
}

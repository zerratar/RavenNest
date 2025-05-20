using RavenNest.DataAnnotations;
using System;

namespace RavenNest.DataModels
{
    public partial class ItemRecipe : Entity<ItemRecipe>
    {
        [PersistentData] private string name;
        [PersistentData] private string description;
        [PersistentData] private Guid itemId;
        [PersistentData] private Guid? failedItemId;
        [PersistentData] private double minSuccessRate;
        [PersistentData] private double maxSuccessRate;
        [PersistentData] private double preparationTime;
        [PersistentData] private bool fixedSuccessRate;
        [PersistentData] private int requiredLevel;
        [PersistentData] private int requiredSkill;
        [PersistentData] public int amount;
    }
}

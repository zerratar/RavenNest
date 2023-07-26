using System;

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

        public int RequiredLevel { get; set; }
        public int RequiredSkill { get; set; }


    }
}

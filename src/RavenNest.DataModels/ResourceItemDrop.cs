using System;

namespace RavenNest.DataModels
{
    public partial class ResourceItemDrop : Entity<ResourceItemDrop>
    {
        private Guid itemId; public Guid ItemId { get => itemId; set => Set(ref itemId, value); }
        private string itemName; public string ItemName { get => itemName; set => Set(ref itemName, value); }
        private double dropChance; public double DropChance { get => dropChance; set => Set(ref dropChance, value); }
        private int levelRequirement; public int LevelRequirement { get => levelRequirement; set => Set(ref levelRequirement, value); }
        private int? skill; public int? Skill { get => skill; set => Set(ref skill, value); }
        private double? cooldown; public double? Cooldown { get => cooldown; set => Set(ref cooldown, value); }
    }
}

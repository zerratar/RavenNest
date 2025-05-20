using RavenNest.DataAnnotations;
using System;

namespace RavenNest.DataModels
{
    public partial class ResourceItemDrop : Entity<ResourceItemDrop>
    {
        [PersistentData] private Guid itemId;
        [PersistentData] private string itemName;
        [PersistentData] private double dropChance;
        [PersistentData] private int levelRequirement;
        [PersistentData] private int? skill;
        [PersistentData] private double? cooldown;
    }
}

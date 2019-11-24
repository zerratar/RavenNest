using System;
using System.Collections.Generic;

namespace RavenNest.Models
{
    public class Item
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public int Level { get; set; }
        public int WeaponAim { get; set; }
        public int WeaponPower { get; set; }
        public int ArmorPower { get; set; }
        public int RequiredAttackLevel { get; set; }
        public int RequiredDefenseLevel { get; set; }
        public ItemCategory Category { get; set; }
        public ItemType Type { get; set; }
        public ItemMaterial Material { get; set; }
        public string MaleModelId { get; set; }
        public string FemaleModelId { get; set; }
        public string GenericPrefab { get; set; }
        public string MalePrefab { get; set; }
        public string FemalePrefab { get; set; }
        public bool? IsGenericModel { get; set; }
        public bool? Craftable { get; set; }
        public int RequiredCraftingLevel { get; set; }
        public long WoodCost { get; set; }
        public long OreCost { get; set; }
        public List<ItemCraftingRequirement> CraftingRequirements { get; set; }
    }
}
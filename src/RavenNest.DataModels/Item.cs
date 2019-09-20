using System;
using System.Collections.Generic;

namespace RavenNest.DataModels
{
    public partial class Item
    {
        public Item()
        {
            InventoryItem = new HashSet<InventoryItem>();
            MarketItem = new HashSet<MarketItem>();
        }

        public Guid Id { get; set; }
        public string Name { get; set; }
        public int Category { get; set; }
        public int Type { get; set; }
        public int Level { get; set; }
        public int WeaponAim { get; set; }
        public int WeaponPower { get; set; }
        public int ArmorPower { get; set; }
        public int RequiredAttackLevel { get; set; }
        public int RequiredDefenseLevel { get; set; }
        public int Material { get; set; }
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

        public ICollection<MarketItem> MarketItem { get; set; }
        public ICollection<InventoryItem> InventoryItem { get; set; }
    }
}

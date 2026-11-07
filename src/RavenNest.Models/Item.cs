using System;
using System.Collections.Generic;

namespace RavenNest.Models
{
    public class Item
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public int Level { get; set; }
        public int WeaponAim { get; set; }
        public int WeaponPower { get; set; }
        public int MagicAim { get; set; }
        public int MagicPower { get; set; }
        public int RangedAim { get; set; }
        public int RangedPower { get; set; }
        public int ArmorPower { get; set; }

        /// <summary>
        /// Every gear stat of the item in RavenNest.Combat.GearStats' text form
        /// ("MELEE_POWER:120;MELEE_ACCURACY:95;FIRE_DAMAGE:30%"): the columns above as power, accuracy and armour,
        /// and what they cannot hold (element damage, resistances, evasion, health, crits). Combat v2 reads this;
        /// games before it read the columns. Null from servers before it.
        /// </summary>
        public string Stats { get; set; }
        public int RequiredAttackLevel { get; set; }
        public int RequiredDefenseLevel { get; set; }
        public int RequiredMagicLevel { get; set; }
        public int RequiredRangedLevel { get; set; }
        public int RequiredSlayerLevel { get; set; }
        public ItemCategory Category { get; set; }
        public ItemType Type { get; set; }
        public ItemMaterial Material { get; set; }
        public HeadMask HeadMask { get; set; }
        public string MaleModelId { get; set; }
        public string FemaleModelId { get; set; }
        public string GenericPrefab { get; set; }
        public string MalePrefab { get; set; }
        public string FemalePrefab { get; set; }
        public bool IsGenericModel { get; set; }

        public long ShopBuyPrice { get; set; }
        public long ShopSellPrice { get; set; }

        //[Obsolete("Do not use, use Item Recipes instead.")] public List<ItemCraftingRequirement> CraftingRequirements { get; set; }
        public bool Soulbound { get; set; }

        public DateTime? Modified { get; set; }
    }

    [Flags]
    public enum HeadMask : int
    {
        None = 0,
        Hair = 1,
        Brows = 2,
        Beard = 4,
        Ears = 8,
        Head = 16,
        All = Hair | Brows | Beard | Ears | Head
    }
}

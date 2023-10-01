using System;

namespace RavenNest.DataModels
{
    public partial class Item : Entity<Item>
    {
        public string Name { get => name; set => Set(ref name, value); }
        public string Description { get => description; set => Set(ref description, value); }
        public int Category { get => category; set => Set(ref category, value); }
        public int Type { get => type; set => Set(ref type, value); }
        public int Level { get => level; set => Set(ref level, value); }
        public int WeaponAim { get => weaponAim; set => Set(ref weaponAim, value); }
        public int WeaponPower { get => weaponPower; set => Set(ref weaponPower, value); }
        public int MagicAim { get => magicAim; set => Set(ref magicAim, value); }
        public int MagicPower { get => magicPower; set => Set(ref magicPower, value); }
        public int RangedAim { get => rangedAim; set => Set(ref rangedAim, value); }
        public int RangedPower { get => rangedPower; set => Set(ref rangedPower, value); }
        public int ArmorPower { get => armorPower; set => Set(ref armorPower, value); }
        public int RequiredAttackLevel { get => requiredAttackLevel; set => Set(ref requiredAttackLevel, value); }
        public int RequiredDefenseLevel { get => requiredDefenseLevel; set => Set(ref requiredDefenseLevel, value); }
        public int RequiredMagicLevel { get => requiredMagicLevel; set => Set(ref requiredMagicLevel, value); }
        public int RequiredRangedLevel { get => requiredRangedLevel; set => Set(ref requiredRangedLevel, value); }
        public int RequiredSlayerLevel { get => requiredSlayerLevel; set => Set(ref requiredSlayerLevel, value); }
        public int Material { get => material; set => Set(ref material, value); }
        public string MaleModelId { get => maleModelId; set => Set(ref maleModelId, value); }
        public string FemaleModelId { get => femaleModelId; set => Set(ref femaleModelId, value); }
        public string GenericPrefab { get => genericPrefab; set => Set(ref genericPrefab, value); }
        public string MalePrefab { get => malePrefab; set => Set(ref malePrefab, value); }
        public string FemalePrefab { get => femalePrefab; set => Set(ref femalePrefab, value); }
        public bool IsGenericModel { get => isGenericModel; set => Set(ref isGenericModel, value); }
        public bool Craftable { get => craftable; set => Set(ref craftable, value); }

        [Obsolete("Should be removed, use Item Recipes instead.")] public int RequiredCraftingLevel { get => requiredCraftingLevel; set => Set(ref requiredCraftingLevel, value); }
        [Obsolete("Should be removed, use Item Recipes instead.")] public int RequiredCookingLevel { get => requiredCraftingLevel; set => Set(ref requiredCookingLevel, value); }

        [Obsolete("Should be removed, use Item Recipes instead.")] public long WoodCost { get => woodCost; set => Set(ref woodCost, value); }

        [Obsolete("Should be removed, use Item Recipes instead.")] public long OreCost { get => oreCost; set => Set(ref oreCost, value); }

        /// <summary>
        /// Amount it costs to buy this item from the Shop.
        /// </summary>
        public long ShopBuyPrice { get => shopBuyPrice; set => Set(ref shopBuyPrice, value); }
        /// <summary>
        /// Vendor Amount, how much a player will receive by vendoring this item
        /// </summary>
        public long ShopSellPrice { get => shopSellPrice; set => Set(ref shopSellPrice, value); }
        public bool Soulbound { get => soulbound; set => Set(ref soulbound, value); }
        public bool Hidden { get => hidden; set => Set(ref hidden, value); }
        public int HeadMask { get => headMask; set => Set(ref headMask, value); }
        public DateTime? Modified { get => modified; set => Set(ref modified, value); }

        private string name;
        private string description;
        private int category;
        private int type;
        private int level;
        private int weaponAim;
        private int weaponPower;
        private int magicAim;
        private int magicPower;
        private int rangedAim;
        private int rangedPower;
        private int armorPower;
        private int requiredAttackLevel;
        private int requiredDefenseLevel;
        private int requiredMagicLevel;
        private int requiredRangedLevel;
        private int requiredSlayerLevel;
        private int material;
        private string maleModelId;
        private string femaleModelId;
        private string genericPrefab;
        private string malePrefab;
        private string femalePrefab;
        private bool isGenericModel;
        private bool craftable;
        private int requiredCraftingLevel;
        private int requiredCookingLevel;
        private long woodCost;
        private long oreCost;
        private long shopBuyPrice;
        private long shopSellPrice;
        private bool soulbound;
        private bool hidden;
        private int headMask;
        private DateTime? modified;
    }
}

using RavenNest.DataAnnotations;
using System;

namespace RavenNest.DataModels
{
    public partial class Item : Entity<Item>
    {
        [PersistentData] private string name;
        [PersistentData] private string description;
        [PersistentData] private int category;
        [PersistentData] private int type;
        [PersistentData] private int level;
        [PersistentData] private int weaponAim;
        [PersistentData] private int weaponPower;
        [PersistentData] private int magicAim;
        [PersistentData] private int magicPower;
        [PersistentData] private int rangedAim;
        [PersistentData] private int rangedPower;
        [PersistentData] private int armorPower;
        [PersistentData] private int requiredAttackLevel;
        [PersistentData] private int requiredDefenseLevel;
        [PersistentData] private int requiredMagicLevel;
        [PersistentData] private int requiredRangedLevel;
        [PersistentData] private int requiredSlayerLevel;
        [PersistentData] private int material;
        [PersistentData] private string maleModelId;
        [PersistentData] private string femaleModelId;
        [PersistentData] private string genericPrefab;
        [PersistentData] private string malePrefab;
        [PersistentData] private string femalePrefab;
        [PersistentData] private bool isGenericModel;
        [PersistentData] private bool craftable;
        [PersistentData] private int requiredCraftingLevel;
        [PersistentData] private int requiredCookingLevel;
        [PersistentData] private long woodCost;
        [PersistentData] private long oreCost;
        /// <summary>
        /// Amount it costs to buy this item from the Shop.
        /// </summary>
        [PersistentData] private long shopBuyPrice;
        /// <summary>
        /// Vendor Amount, how much a player will receive by vendoring this item
        /// </summary>
        [PersistentData] private long shopSellPrice;
        [PersistentData] private bool soulbound;
        [PersistentData] private bool hidden;
        [PersistentData] private int headMask;
        [PersistentData] private DateTime? modified;
    }
}

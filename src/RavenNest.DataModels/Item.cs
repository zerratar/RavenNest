using System;

namespace RavenNest.DataModels
{
    public enum PetType
    {
        FantasyDeer,
        Deer,
        Bear,
        Wolf,
        Fox,
        Boar,
        Hare,
        Moose
    }

    public enum PetTier
    {
        Tier1,
        Tier2,
        Tier3
    }

    public partial class Pet : Entity<Pet>
    {
        private Guid id; public Guid Id { get => id; set => Set(ref id, value); }
        private Guid characterId; public Guid CharacterId { get => characterId; set => Set(ref characterId, value); }
        private PetType type; public PetType Type { get => type; set => Set(ref type, value); }
        private PetTier tier; public PetTier Tier { get => tier; set => Set(ref tier, value); }
        private string name; public string Name { get => name; set => Set(ref name, value); }
        private DateTime dateOfBirth; public DateTime DateOfBirth { get => dateOfBirth; set => Set(ref dateOfBirth, value); }
        private int level; public int Level { get => level; set => Set(ref level, value); }
        private long experience; public long Experience { get => experience; set => Set(ref experience, value); }
        private int attack; public int Attack { get => attack; set => Set(ref attack, value); }
        private int defense; public int Defense { get => defense; set => Set(ref defense, value); }
        private int strength; public int Strength { get => strength; set => Set(ref strength, value); }
        private int health; public int Health { get => health; set => Set(ref health, value); }
        private int currentHealth; public int CurrentHealth { get => currentHealth; set => Set(ref currentHealth, value); }
        private float happiness; public float Happiness { get => happiness; set => Set(ref happiness, value); }
        private float hunger; public float Hunger { get => hunger; set => Set(ref hunger, value); }
        private string prefab; public string Prefab { get => prefab; set => Set(ref prefab, value); }
        private TimeSpan playTime; public TimeSpan PlayTime { get => playTime; set => Set(ref playTime, value); }
        private bool active; public bool Active { get => active; set => Set(ref active, value); }
    }

    public partial class Item : Entity<Item>
    {
        private Guid id; public Guid Id { get => id; set => Set(ref id, value); }
        private string name; public string Name { get => name; set => Set(ref name, value); }
        private int category; public int Category { get => category; set => Set(ref category, value); }
        private int type; public int Type { get => type; set => Set(ref type, value); }
        private int level; public int Level { get => level; set => Set(ref level, value); }
        private int weaponAim; public int WeaponAim { get => weaponAim; set => Set(ref weaponAim, value); }
        private int weaponPower; public int WeaponPower { get => weaponPower; set => Set(ref weaponPower, value); }
        private int magicAim; public int MagicAim { get => magicAim; set => Set(ref magicAim, value); }
        private int magicPower; public int MagicPower { get => magicPower; set => Set(ref magicPower, value); }
        private int rangedAim; public int RangedAim { get => rangedAim; set => Set(ref rangedAim, value); }
        private int rangedPower; public int RangedPower { get => rangedPower; set => Set(ref rangedPower, value); }
        private int armorPower; public int ArmorPower { get => armorPower; set => Set(ref armorPower, value); }
        private int requiredAttackLevel; public int RequiredAttackLevel { get => requiredAttackLevel; set => Set(ref requiredAttackLevel, value); }
        private int requiredDefenseLevel; public int RequiredDefenseLevel { get => requiredDefenseLevel; set => Set(ref requiredDefenseLevel, value); }
        private int requiredMagicLevel; public int RequiredMagicLevel { get => requiredMagicLevel; set => Set(ref requiredMagicLevel, value); }
        private int requiredRangedLevel; public int RequiredRangedLevel { get => requiredRangedLevel; set => Set(ref requiredRangedLevel, value); }
        private int requiredSlayerLevel; public int RequiredSlayerLevel { get => requiredSlayerLevel; set => Set(ref requiredSlayerLevel, value); }
        private int material; public int Material { get => material; set => Set(ref material, value); }
        private string maleModelId; public string MaleModelId { get => maleModelId; set => Set(ref maleModelId, value); }
        private string femaleModelId; public string FemaleModelId { get => femaleModelId; set => Set(ref femaleModelId, value); }
        private string genericPrefab; public string GenericPrefab { get => genericPrefab; set => Set(ref genericPrefab, value); }
        private string malePrefab; public string MalePrefab { get => malePrefab; set => Set(ref malePrefab, value); }
        private string femalePrefab; public string FemalePrefab { get => femalePrefab; set => Set(ref femalePrefab, value); }
        private bool? isGenericModel; public bool? IsGenericModel { get => isGenericModel; set => Set(ref isGenericModel, value); }
        private bool? craftable; public bool? Craftable { get => craftable; set => Set(ref craftable, value); }
        private int requiredCraftingLevel; public int RequiredCraftingLevel { get => requiredCraftingLevel; set => Set(ref requiredCraftingLevel, value); }

        // Todo: remove
        private long woodCost; public long WoodCost { get => woodCost; set => Set(ref woodCost, value); }

        // Todo: remove
        private long oreCost; public long OreCost { get => oreCost; set => Set(ref oreCost, value); }

        // Todo: remove
        private long shopBuyPrice;
        /// <summary>
        /// Amount it costs to buy this item from the Shop.
        /// </summary>
        public long ShopBuyPrice { get => shopBuyPrice; set => Set(ref shopBuyPrice, value); }

        // Todo: remove

        private long shopSellPrice;

        /// <summary>
        /// Vendor Amount, how much a player will receive by vendoring this item
        /// </summary>
        public long ShopSellPrice { get => shopSellPrice; set => Set(ref shopSellPrice, value); }
        private bool? soulbound; public bool? Soulbound { get => soulbound; set => Set(ref soulbound, value); }
        private bool? hidden; public bool? Hidden { get => hidden; set => Set(ref hidden, value); }

        // private bool stackable; public bool Stackable { get => stackable; set => Set(ref stackable, value); }
    }
}

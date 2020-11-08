using System;
using ItemCategory = RavenNest.Models.ItemCategory;
using ItemMaterial = RavenNest.Models.ItemMaterial;
using ItemType = RavenNest.Models.ItemType;

namespace RavenNest
{
    public class ItemDefinition
    {
        public ItemDefinition(
            Guid id,
            string name,
            int level,
            int weaponAim,
            int weaponPower,
            int armorPower,
            int requiredAttackLevel,
            int requiredDefenseLevel,
            ItemCategory category,
            ItemType type,
            ItemMaterial material,
            string maleModelId,
            string femaleModelId,
            string genericPrefab,
            string malePrefab,
            string femalePrefab,
            bool isGenericModel)
        {
            Id = id;
            Name = name;
            Level = level;
            WeaponAim = weaponAim;
            WeaponPower = weaponPower;
            ArmorPower = armorPower;
            RequiredAttackLevel = requiredAttackLevel;
            RequiredDefenseLevel = requiredDefenseLevel;
            GenericPrefab = genericPrefab;
            MalePrefab = malePrefab;
            FemalePrefab = femalePrefab;
            IsGenericModel = isGenericModel;
            Category = category;
            Type = type;
            Material = material;
            MaleModelId = maleModelId;
            FemaleModelId = femaleModelId;
        }

        public Guid Id { get; }
        public string Name { get; }
        public int Level { get; }
        public int WeaponAim { get; }
        public int WeaponPower { get; }
        public int ArmorPower { get; }
        public int RequiredAttackLevel { get; }
        public int RequiredDefenseLevel { get; }

        public ItemCategory Category { get; }
        public ItemType Type { get; }
        public ItemMaterial Material { get; }

        public string MaleModelId { get; }
        public string FemaleModelId { get; }

        public string GenericPrefab { get; }
        public string MalePrefab { get; }
        public string FemalePrefab { get; }

        public bool IsGenericModel { get; }

        public ItemDefinition With(string genericPrefab = null)
        {
            return new ItemDefinition(
                this.Id,
                this.Name,
                this.Level,
                this.WeaponAim,
                this.WeaponPower,
                this.ArmorPower,
                this.RequiredAttackLevel,
                this.RequiredDefenseLevel,
                this.Category,
                this.Type,
                this.Material,
                this.MaleModelId,
                this.FemaleModelId,
                genericPrefab ?? this.GenericPrefab,
                this.MalePrefab,
                this.FemalePrefab,
                this.IsGenericModel);
        }


        public int GetTotalStats()
        {
            return this.WeaponPower + this.WeaponAim + this.ArmorPower;
        }
    }
}

using RavenNest.BusinessLogic.Data;
using RavenNest.DataModels;
using System;

namespace RavenNest.BusinessLogic.Game
{

    public struct ReadOnlyInventoryItem
    {
        public Guid Id { get; }
        public Guid ItemId { get; }
        public Guid? TransmogrificationId { get; }
        public string Name { get; }
        public string Enchantment { get; }
        public int Flags { get; }
        public long Amount { get; }
        public bool Equipped { get; }
        public string Tag { get; }
        public bool Soulbound { get; }
        public EquipmentSlot EquipmentSlot { get; }
        public Item Item { get; }
        private ReadOnlyInventoryItem(
            Guid id,
            Guid itemId,
            Guid? transmogrificationId,
            string name,
            string enchantment,
            long amount,
            bool equipped,
            string tag,
            int flags,
            bool soulbound,
            Item item,
            EquipmentSlot equipmentSlot)
        {
            Id = id;
            ItemId = itemId;
            TransmogrificationId = transmogrificationId;
            Name = name;
            Flags = flags;
            Enchantment = enchantment;
            Amount = amount;
            Equipped = equipped;
            Item = item;
            Tag = tag;
            EquipmentSlot = equipmentSlot;
            Soulbound = soulbound;
        }

        public static ReadOnlyInventoryItem Create(GameData gameData, InventoryItem item)
        {
            var i = gameData.GetItem(item.ItemId);
            if (i == null)
                return new ReadOnlyInventoryItem();
            return new ReadOnlyInventoryItem(
                item.Id,
                item.ItemId,
                item.TransmogrificationId,
                item.Name,
                item.Enchantment,
                item.Amount ?? 1,
                item.Equipped,
                item.Tag,
                item.Flags.GetValueOrDefault(),
                item.Soulbound,
                i,
                GetEquipmentSlot((ItemType)i.Type));
        }

        public static EquipmentSlot GetEquipmentSlot(ItemType type)
        {
            switch (type)
            {
                case ItemType.Amulet: return EquipmentSlot.Amulet;
                case ItemType.Ring: return EquipmentSlot.Ring;
                case ItemType.Shield: return EquipmentSlot.Shield;
                case ItemType.Hat: return EquipmentSlot.Head;
                case ItemType.Mask: return EquipmentSlot.Head;
                case ItemType.Helmet: return EquipmentSlot.Head;
                case ItemType.HeadCovering: return EquipmentSlot.Head;
                case ItemType.Chest: return EquipmentSlot.Chest;
                case ItemType.Gloves: return EquipmentSlot.Gloves;
                case ItemType.Leggings: return EquipmentSlot.Leggings;
                case ItemType.Boots: return EquipmentSlot.Boots;
                case ItemType.Pet: return EquipmentSlot.Pet;
                case ItemType.OneHandedAxe: return EquipmentSlot.MeleeWeapon;
                case ItemType.TwoHandedAxe: return EquipmentSlot.MeleeWeapon;
                case ItemType.TwoHandedSpear: return EquipmentSlot.MeleeWeapon;
                case ItemType.OneHandedSword: return EquipmentSlot.MeleeWeapon;
                case ItemType.TwoHandedSword: return EquipmentSlot.MeleeWeapon;
                case ItemType.TwoHandedStaff: return EquipmentSlot.MagicWeapon;
                case ItemType.TwoHandedBow: return EquipmentSlot.RangedWeapon;
            }

            return EquipmentSlot.None;
        }

        internal InventoryItem AsUntrackedInventoryItem(Guid characterId)
        {
            return new InventoryItem
            {
                Id = Id,
                ItemId = ItemId,
                TransmogrificationId = TransmogrificationId,
                Name = Name,
                Enchantment = Enchantment,
                Amount = Amount,
                Equipped = Equipped,
                Tag = Tag,
                Flags = Flags,
                Soulbound = Soulbound,
                CharacterId = characterId
            };
        }
    }
}

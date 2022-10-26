using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RavenNest.BusinessLogic.Game;
using RavenNest.BusinessLogic.Providers;
using RavenNest.Models;

namespace RavenNest.BusinessLogic.Extended
{
    public class ItemInstance
    {
        private InventoryItem InventoryItem { get; set; }
        private UserBankItem UserBankItem { get; set; }
        public Item ItemInfo { get; set; }
        private IReadOnlyList<DataModels.ItemAttribute> AvailableAttributes { get; set; }
        private bool IsStashed { get { return UserBankItem != null; } }
        private Providers.EquipmentSlot? _equipmentSlot;
        public Guid Id { get { return IsStashed ? UserBankItem.Id : InventoryItem.Id; } }
        public Guid ItemId { get { return IsStashed ? UserBankItem.ItemId : InventoryItem.ItemId; } }
        public Guid OwnerId { get { return IsStashed ? UserBankItem.UserId : InventoryItem.CharacterId; } }
        public string Name { get { return IsStashed ? UserBankItem.Name : InventoryItem.Name; } }
        public bool Equipped { get { return IsStashed ? false : InventoryItem.Equipped; } }
        public long Amount { get { return IsStashed ? UserBankItem.Amount : InventoryItem.Amount; } }
        public string Tag { get { return IsStashed ? UserBankItem.Tag : InventoryItem.Tag; } }
        public bool? Soulbound { get { return IsStashed ? UserBankItem.Soulbound : InventoryItem.Soulbound; } }
        public string Enchantment { get { return IsStashed ? UserBankItem.Enchantment : InventoryItem.Enchantment; } }
        public object InvItem { get { return IsStashed ? UserBankItem : InventoryItem; } }
        public Providers.EquipmentSlot EquipmentSlot { get { return _equipmentSlot ?? Providers.EquipmentSlot.None; } }
        public Location Location
        {
            get
            {
                if (IsStashed)
                {
                    return Location.Bank;
                }
                else if (InventoryItem.Equipped)
                {
                    return Location.Equipment;
                }
                else
                {
                    return Location.CharactersBag;
                }
            }
        }
        public string GetItemRequirementLevel()
        {
            if (ItemInfo.RequiredAttackLevel > 0) return ItemInfo.RequiredAttackLevel.ToString();
            if (ItemInfo.RequiredDefenseLevel > 0) return ItemInfo.RequiredDefenseLevel.ToString();
            if (ItemInfo.RequiredRangedLevel > 0) return ItemInfo.RequiredRangedLevel.ToString();
            if (ItemInfo.RequiredMagicLevel > 0) return ItemInfo.RequiredMagicLevel.ToString();
            return "";
        }
        public string GetItemRequirementSkill()
        {
            if (ItemInfo.RequiredAttackLevel > 0) return "Requires Attack Level";
            if (ItemInfo.RequiredDefenseLevel > 0) return "Requires Defense Level";
            if (ItemInfo.RequiredRangedLevel > 0) return "Requires Ranged Level";
            if (ItemInfo.RequiredMagicLevel > 0) return "Requires Magic Level";
            return "";
        }
        public string GetItemAmount()
        {
            var value = Amount;
            if (value >= 1000_000)
            {
                var mils = value / 1000000.0;
                return Math.Round(mils) + "M";
            }
            else if (value > 1000)
            {
                var ks = value / 1000m;
                return Math.Round(ks) + "K";
            }
            else if (value > 1)
            {

                return Amount.ToString();
            }

            return "";
        }
        public string GetItemImageSrc()
        {
            if (Tag != null)
            {
                return $"/api/twitch/logo/{Tag}";
            }
            return $"/imgs/items/{ItemId}.png";
        }
        public string GetItemTier()
        {
            if (ItemInfo.Type == ItemType.Pet) return "pet";
            if (ItemInfo.RequiredMagicLevel == 100 || ItemInfo.RequiredRangedLevel == 100 || ItemInfo.RequiredAttackLevel == 100 || ItemInfo.RequiredDefenseLevel == 100) return "8";
            if (ItemInfo.RequiredMagicLevel >= 120 || ItemInfo.RequiredRangedLevel >= 120 || ItemInfo.RequiredAttackLevel >= 120 || ItemInfo.RequiredDefenseLevel >= 120) return "9";
            return ItemInfo.Material.ToString();
        }
        public IReadOnlyList<ItemEnchantmentInfo> GetItemEnchantments()
        {
            var enchantments = new List<ItemEnchantmentInfo>();
            if (!string.IsNullOrEmpty(Enchantment))
            {
                var en = Enchantment.Split(';');
                foreach (var e in en)
                {
                    var data = e.Split(':');
                    var key = data[0];
                    var value = PlayerInventory.GetValue(data[1], out var type);
                    var attr = AvailableAttributes.FirstOrDefault(x => x.Name == key);
                    var description = "";

                    if (type == AttributeValueType.Percent)
                    {
                        if (attr != null)
                        {
                            description = attr.Description.Replace(attr.MaxValue, value + "%");
                        }
                        value = value / 100d;
                    }
                    else
                    {
                        if (attr != null)
                        {
                            description = attr.Description.Replace(attr.MaxValue, value.ToString());
                        }
                    }

                    enchantments.Add(new ItemEnchantmentInfo
                    {
                        Name = key,
                        Value = value,
                        ValueType = type,
                        Description = description,
                    });
                }
            }
            return enchantments;
        }

        public IReadOnlyList<ItemStat> GetItemStats()
        {
            var stats = new List<ItemStat>();
            var i = ItemInfo;

            int aimBonus = 0;
            int armorBonus = 0;
            int powerBonus = 0;

            if (!string.IsNullOrEmpty(Enchantment))
            {
                var enchantments = Enchantment.ToLower().Split(';');
                foreach (var e in enchantments)
                {
                    var value = PlayerInventory.GetValue(e, out var type);
                    var key = e.Split(':')[0];
                    if (type == AttributeValueType.Percent)
                    {
                        value = value / 100d;
                        if (key == "power") powerBonus = (int)(i.WeaponPower * value) + (int)(i.MagicPower * value) + (int)(i.RangedPower * value);
                        if (key == "aim") aimBonus = (int)(i.WeaponAim * value) + (int)(i.MagicAim * value) + (int)(i.RangedAim * value);
                        if (key == "armor" || key == "armour") armorBonus = (int)(i.ArmorPower * value);
                    }
                    else
                    {
                        if (key == "power") powerBonus = (int)value;
                        if (key == "aim") aimBonus = (int)value;
                        if (key == "armor" || key == "armour") armorBonus = (int)value;
                    }
                }
            }

            if (i.WeaponAim > 0) stats.Add(new ItemStat("Weapon Aim", i.WeaponAim, aimBonus));
            if (i.WeaponPower > 0) stats.Add(new ItemStat("Weapon Power", i.WeaponPower, powerBonus));
            if (i.RangedAim > 0) stats.Add(new ItemStat("Ranged Aim", i.RangedAim, aimBonus));
            if (i.RangedPower > 0) stats.Add(new ItemStat("Ranged Power", i.RangedPower, powerBonus));
            if (i.MagicAim > 0) stats.Add(new ItemStat("Magic Aim", i.MagicAim, aimBonus));
            if (i.MagicPower > 0) stats.Add(new ItemStat("Magic Power", i.MagicPower, powerBonus));
            if (i.ArmorPower > 0) stats.Add(new ItemStat("Armor", i.ArmorPower, armorBonus));

            return stats;
        }

        public ItemInstance(Item item, InventoryItem inventoryItem, Providers.EquipmentSlot equipmentSlot, IReadOnlyList<DataModels.ItemAttribute> availableAttributes)
        {
            SetItemAndSlot(item, equipmentSlot, availableAttributes);
            UpdatetItemInstance(inventoryItem);
        }

        private void SetItemAndSlot(Item item, Providers.EquipmentSlot equipmentSlot, IReadOnlyList<DataModels.ItemAttribute> availableAttributes)
        {
            ItemInfo = item;
            AvailableAttributes = availableAttributes;
            _equipmentSlot = equipmentSlot;
        }

        public ItemInstance(Item item, UserBankItem userBankItem, Providers.EquipmentSlot equipmentSlot, IReadOnlyList<DataModels.ItemAttribute> availableAttributes)
        {
            SetItemAndSlot(item, equipmentSlot, availableAttributes);
            UpdatetItemInstance(userBankItem);
        }

        public void UpdatetItemInstance(InventoryItem inventoryItem)
        {
            InventoryItem = inventoryItem;
            UserBankItem = null;
        }

        public void UpdatetItemInstance(UserBankItem userBankItem)
        {
            UserBankItem = userBankItem;
            InventoryItem = null;
        }
        public string GetEnchantmentValue(ItemEnchantmentInfo enchantment)
        {
            var value = enchantment.Value;
            if (enchantment.ValueType == AttributeValueType.Percent)
            {
                return ((int)(value * 100)) + "%";
            }

            return ((int)value).ToString();
        }

        public string GetEnchantmentName(ItemEnchantmentInfo enchantment)
        {
            return enchantment.Name[0] + enchantment.Name.ToLower().Substring(1);
        }
    }

    public enum Location
    {
        Bank,
        Equipment,
        CharactersBag
    }
}

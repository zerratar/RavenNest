using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RavenNest.Models;

namespace RavenNest.BusinessLogic.Extended
{
    public class ItemInstance
    {
        private InventoryItem InventoryItem { get; set; }
        private UserBankItem UserBankItem { get; set; }
        public Item ItemInfo { get; set; }
        private bool IsStashed { get { return UserBankItem != null; } }
        private Providers.EquipmentSlot? _equipmentSlot;
        public Guid Id { get { return IsStashed ? UserBankItem.Id : InventoryItem.Id; } }
        public Guid ItemId { get { return IsStashed ? UserBankItem.ItemId : InventoryItem.ItemId; } }
        public Guid OwnerId { get { return IsStashed ? UserBankItem.UserId : InventoryItem.CharacterId; } }
        public string Name { get { return IsStashed ? UserBankItem.Name : InventoryItem.Name; } }
        public bool Equipped { get { return IsStashed ? false : InventoryItem.Equipped; } }
        public long? Amount { get { return IsStashed ? UserBankItem.Amount : InventoryItem.Amount; } }
        public string Tag { get { return IsStashed ? UserBankItem.Tag : InventoryItem.Tag; } }
        public object GetItem { get { return IsStashed ? UserBankItem : InventoryItem; } }
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
        public ItemInstance(Item item, InventoryItem inventoryItem, Providers.EquipmentSlot equipmentSlot)
        {
            ItemInfo = item;
            SetItemInstance(inventoryItem, equipmentSlot);
        }
        public ItemInstance(Item item, UserBankItem userBankItem, Providers.EquipmentSlot equipmentSlot)
        {
            ItemInfo = item;
            SetItemInstance(userBankItem, equipmentSlot);
        }

        public void SetItemInstance(InventoryItem inventoryItem, Providers.EquipmentSlot equipmentSlot)
        {
            InventoryItem = inventoryItem;
            _equipmentSlot = equipmentSlot;
            UserBankItem = null;
        }

        public void SetItemInstance(UserBankItem userBankItem, Providers.EquipmentSlot equipmentSlot)
        {
            UserBankItem = userBankItem;
            _equipmentSlot = equipmentSlot;
            InventoryItem = null;
        }

        public void SetItemInstance(ItemInstance update)
        {
            if(update.IsStashed)
            {
                UpdatetItemInstance((UserBankItem)update.GetItem);
            }
            else
            {
                UpdatetItemInstance((InventoryItem)update.GetItem);
            }
            
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
    }

    public enum Location
    {
        Bank,
        Equipment,
        CharactersBag
    }
}

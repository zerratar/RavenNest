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
        private bool IsStashed { get { return UserBankItem != null; } }
        private Providers.EquipmentSlot? _equipmentSlot;
        public Guid Id { get { return IsStashed ? UserBankItem.Id : InventoryItem.Id; } }
        public Guid ItemId { get { return IsStashed ? UserBankItem.ItemId : InventoryItem.ItemId; } }
        public Guid OwnerId { get { return IsStashed ? UserBankItem.UserId : InventoryItem.CharacterId; } }
        public string Name { get { return IsStashed ? UserBankItem.Name : InventoryItem.Name; } }
        public bool Equipped { get { return IsStashed ? false : InventoryItem.Equipped; } }
        public long? Amount { get { return IsStashed ? UserBankItem.Amount : InventoryItem.Amount; } }
        public string Tag { get { return IsStashed ? UserBankItem.Tag : InventoryItem.Tag; } }
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
        public ItemInstance(InventoryItem inventoryItem, Providers.EquipmentSlot equipmentSlot)
        {
            setItemInstance(inventoryItem, equipmentSlot);
        }
        public ItemInstance(UserBankItem userBankItem, Providers.EquipmentSlot equipmentSlot)
        {
            setItemInstance(userBankItem, equipmentSlot);
        }

        public void setItemInstance(InventoryItem inventoryItem, Providers.EquipmentSlot equipmentSlot)
        {
            InventoryItem = inventoryItem;
            _equipmentSlot = equipmentSlot;
            UserBankItem = null;
        }

        public void setItemInstance(UserBankItem userBankItem, Providers.EquipmentSlot equipmentSlot)
        {
            UserBankItem = userBankItem;
            _equipmentSlot = equipmentSlot;
            InventoryItem = null;
        }

        public void updatetItemInstance(InventoryItem inventoryItem)
        {
            InventoryItem = inventoryItem;
            UserBankItem = null;
        }

        public void updateItemInstance(UserBankItem userBankItem)
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

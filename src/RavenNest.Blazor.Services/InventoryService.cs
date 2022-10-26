using Microsoft.AspNetCore.Http;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Extended;
using RavenNest.BusinessLogic.Game;
using RavenNest.BusinessLogic.Providers;
using RavenNest.Models;
using RavenNest.Sessions;
using System;

namespace RavenNest.Blazor.Services
{
    public class InventoryService : RavenNestService
    {
        private readonly IPlayerManager playerManager;
        private readonly IPlayerInventoryProvider inventoryProvider;

        public InventoryService(
            IPlayerManager playerManager,
            IPlayerInventoryProvider inventoryProvider,
            IHttpContextAccessor accessor,
            ISessionInfoProvider sessionInfoProvider)
            : base(accessor, sessionInfoProvider)
        {
            this.playerManager = playerManager;
            this.inventoryProvider = inventoryProvider;
        }

        public bool UnequipItem(ref ItemInstance itemInstance)
        {

            if (itemInstance.Location.Equals(Location.Bank))
                return false;

            InventoryItem inventoryItem = (InventoryItem)itemInstance.InvItem;
            if (playerManager.UnequipItem(itemInstance.OwnerId, inventoryItem))
            {
                inventoryItem.Equipped = false;
                itemInstance.UpdatetItemInstance(inventoryItem);
                return true;
            }

            return false;
        }

        public void SendToCharacter(Guid newOwnerId, ref ItemInstance itemInstance)
        {
            if (!IsNewOwner(newOwnerId, itemInstance))
                return;

            if (itemInstance.Location.Equals(Location.Bank))
            {
                var update = playerManager.SendToCharacterGetInventoryItemModel(newOwnerId, (UserBankItem)itemInstance.InvItem, itemInstance.Amount);
                if (update != null)
                    itemInstance.UpdatetItemInstance(update);
            }
            else
            {
                var update = playerManager.SendToCharacterGetInventoryItemModel(itemInstance.OwnerId, newOwnerId, (InventoryItem)itemInstance.InvItem, itemInstance.Amount);
                itemInstance.UpdatetItemInstance(update);
            }

        }

        public void SendToStash(Guid newOwnerId, ref ItemInstance itemInstance)
        {
            var result = playerManager.SendToStashAndGetBankItem(itemInstance.OwnerId, (InventoryItem)itemInstance.InvItem, itemInstance.Amount);
            if(result != null)
                itemInstance.UpdatetItemInstance(result);
        }

        public bool IsNewOwner(Guid newOwnerId, ItemInstance itemInstance)
        {
            return !newOwnerId.Equals(itemInstance.OwnerId);
        }

        public void EquipItem(ref ItemInstance itemInstance)
        {
            var inventoryItem = (InventoryItem)itemInstance.InvItem;
            var result = playerManager.EquipItem(itemInstance.OwnerId, inventoryItem);
            if (result)
            {
                inventoryItem.Equipped = true;
                itemInstance.UpdatetItemInstance(inventoryItem);
            }

        }

        public Guid? GetItemInEquipmentSlot(Guid characterID, RavenNest.BusinessLogic.Providers.EquipmentSlot slot)
        {
            var inventory = inventoryProvider.Get(characterID);

            var existingItem = inventory.GetEquippedItem(slot);

            return existingItem.IsNotNull() ? existingItem.Id : null;
        }
    }
}

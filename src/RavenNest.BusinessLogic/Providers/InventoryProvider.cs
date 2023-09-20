using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Extended;
using RavenNest.DataModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

namespace RavenNest.BusinessLogic.Providers
{
    public class PlayerInventory
    {
        private readonly Guid characterId;
        private readonly ILogger logger;
        private readonly GameData gameData;
        private readonly object mutex = new object();
        private readonly InventoryItemCollection items;

        private readonly HashSet<Guid> locked = new HashSet<Guid>();

        private static readonly TimeSpan PatreonRewardFrequency = TimeSpan.FromDays(1);
        private static readonly Random random = new Random();
        public PlayerInventory(ILogger logger, GameData gameData, Guid characterId)
        {
            this.logger = logger;
            this.gameData = gameData;
            this.characterId = characterId;
            items = new InventoryItemCollection(gameData, GetInventoryItems(characterId));
            //AddPatreonTierRewards();
            //MergeItems(this.items.ToList());
        }

        /// <summary>
        /// Called to check if the inventory items in this inventory matches with gameData
        /// </summary>
        private void ValidateInventory([CallerMemberName] string callerMemberName = null)
        {
            try
            {
                List<InventoryItem> missingItems = null;
                List<InventoryItem> emptyStacks = null;
                List<InventoryItem> duplicateStacks = null;
                List<InventoryItem> itemsWithBadAmount = null;

                var emptyStackCount = 0;
                var duplicatedStackCount = 0;
                var errorMessage = new System.Text.StringBuilder();
                var knownPlayerItems = gameData.GetAllPlayerItems(characterId).ToDictionary(x => x.Id, x => x);
                var itemCountDelta = 0;
                if (knownPlayerItems.Count != items.Count)
                {
                    itemCountDelta = knownPlayerItems.Count - items.Count;
                    errorMessage.AppendLine($"Item count mismatch '{items.Count}' found, expected {knownPlayerItems.Count}.");

                    // check if there are duplicate unequipped stacks
                    // and if there are any unequipped stacks with 0 amount

                    var unequippedStacks = new Dictionary<System.Guid, int>();
                    foreach (var item in knownPlayerItems)
                    {
                        if (item.Value.Equipped) continue;
                        if (unequippedStacks.TryGetValue(item.Value.ItemId, out var i))
                        {
                            unequippedStacks[item.Value.ItemId] = i + 1;
                            if (duplicateStacks == null) duplicateStacks = new List<InventoryItem>();
                            duplicateStacks.Add(item.Value);
                            duplicatedStackCount++;
                        }
                        else
                        {
                            unequippedStacks[item.Value.ItemId] = 1;
                        }

                        if (item.Value.Amount == 0)
                        {
                            emptyStackCount++;
                            if (emptyStacks == null) emptyStacks = new List<InventoryItem>();
                            emptyStacks.Add(item.Value);
                        }
                    }
                }

                for (var i = 0; i < items.Count; i++)
                {
                    var invItem = items[i];
                    if (!knownPlayerItems.TryGetValue(invItem.Id, out var dbItem))
                    {
                        if (missingItems == null) missingItems = new List<InventoryItem>();
                        missingItems.Add(invItem);
                        continue;
                    }

                    if (dbItem.Amount != invItem.Amount)
                    {
                        if (itemsWithBadAmount == null) itemsWithBadAmount = new List<InventoryItem>();
                        itemsWithBadAmount.Add(invItem);
                    }
                }

                if (missingItems != null)
                {
                    errorMessage.AppendLine("======== " + missingItems.Count + " Missing Items =======");
                    errorMessage.AppendLine(Newtonsoft.Json.JsonConvert.SerializeObject(missingItems));
                    foreach (var stack in missingItems)
                    {
                        errorMessage.AppendLine("# " + gameData.GetItem(stack.ItemId).Name);
                        errorMessage.AppendLine(Newtonsoft.Json.JsonConvert.SerializeObject(stack));
                    }
                    errorMessage.AppendLine();
                }

                if (duplicateStacks != null)
                {
                    errorMessage.AppendLine("======== " + duplicateStacks.Count + " Duplicate Items =======");
                    errorMessage.AppendLine(Newtonsoft.Json.JsonConvert.SerializeObject(duplicateStacks));
                    foreach (var stack in duplicateStacks)
                    {
                        errorMessage.AppendLine("# " + gameData.GetItem(stack.ItemId).Name);
                        errorMessage.AppendLine(Newtonsoft.Json.JsonConvert.SerializeObject(stack));
                    }
                    errorMessage.AppendLine();
                }

                if (emptyStacks != null)
                {
                    errorMessage.AppendLine("======== " + emptyStacks.Count + " Empty Items =======");
                    foreach (var stack in emptyStacks)
                    {
                        errorMessage.AppendLine("# " + gameData.GetItem(stack.ItemId).Name);
                        errorMessage.AppendLine(Newtonsoft.Json.JsonConvert.SerializeObject(stack));
                    }
                    errorMessage.AppendLine();
                }

                if (itemsWithBadAmount != null)
                {
                    errorMessage.AppendLine("======== " + itemsWithBadAmount.Count + " Items with wrong amount =======");
                    errorMessage.AppendLine(Newtonsoft.Json.JsonConvert.SerializeObject(itemsWithBadAmount));
                    foreach (var stack in itemsWithBadAmount)
                    {
                        errorMessage.AppendLine("# " + gameData.GetItem(stack.ItemId).Name);
                        errorMessage.AppendLine(Newtonsoft.Json.JsonConvert.SerializeObject(stack));
                    }
                    errorMessage.AppendLine();
                }
                if (errorMessage.Length > 0)
                {
                    var error = errorMessage.ToString();

                    var folder = System.IO.Path.Combine(FolderPaths.GeneratedData, FolderPaths.BadInventory);
                    var dataFolder = new DirectoryInfo(folder);
                    if (!System.IO.Directory.Exists(folder))
                    {
                        dataFolder = Directory.CreateDirectory(folder);
                    }
                    var c = gameData.GetCharacter(characterId);

                    System.IO.File.WriteAllText(Path.Combine(dataFolder.FullName, c.Name + "-" + c.CharacterIndex + ".txt"), error);

                    var badAmountCount = itemsWithBadAmount?.Count ?? 0;
                    var missingItemCount = missingItems?.Count ?? 0;

                    logger.LogError(callerMemberName + " - (Add: " + items.LastAddResult + " Remove: " + items.LastRemoveResult + ")" + c.Name + "#" + c.CharacterIndex + " inventory validation failed. Item Stack Delta: " + itemCountDelta + ", "
                        + duplicatedStackCount + " duplicate stacks, "
                        + emptyStackCount + " empty stacks, "
                        + badAmountCount + " mismatch amount, "
                        + missingItemCount + " missing stacks");
                }
            }
            catch (Exception exc)
            {
                var c = gameData.GetCharacter(characterId);

                logger.LogError(c.Name + "#" + c.CharacterIndex + " inventory validation threw an exception: " + exc);
            }
            // log a short message to db, but save full list to disk.
        }

        private List<InventoryItem> GetInventoryItems(Guid characterId)
        {
            var output = new List<InventoryItem>();
            var equippedTypes = new Dictionary<int, DataModels.InventoryItem>();
            var invItems = gameData?.GetAllPlayerItems(characterId)?.ToList() ?? new List<InventoryItem>();
            foreach (var i in invItems)
            {
                /* Make sure we unequipped items of same type. */

                if (i.Equipped)
                {
                    var itemType = gameData.GetItem(i.ItemId).Type;
                    if (equippedTypes.ContainsKey(itemType))
                    {
                        i.Equipped = false;
                    }
                    else
                    {
                        equippedTypes[itemType] = i;
                    }
                }

                output.Add(i);
            }
            return output;
        }

        public void EquipBestItems()
        {
            try
            {
                lock (mutex)
                {
                    var character = gameData.GetCharacter(characterId);
                    var equippedPetInventoryItem = GetEquippedItem(ItemCategory.Pet);

                    UnequipAllItems();

                    var skills = gameData.GetCharacterSkills(character.SkillsId);
                    var inventoryItems = GetUnequippedItems()
                        //.Select(x => new { InventoryItem = x, Item = x.Item })
                        .Where(x => CanEquipItem(x.Item, skills))
                        .OrderByDescending(x => GetItemValue(x.Item))
                        .ToList();

                    var meleeWeaponToEquip = inventoryItems.FirstOrDefault(x => x.Item.Category == (int)ItemCategory.Weapon && (x.Item.Type == (int)ItemType.OneHandedSword || x.Item.Type == (int)ItemType.TwoHandedSword || x.Item.Type == (int)ItemType.TwoHandedAxe));
                    if (meleeWeaponToEquip.IsNotNull())
                        EquipItem(meleeWeaponToEquip);

                    var rangedWeaponToEquip = inventoryItems.FirstOrDefault(x => x.Item.Category == (int)ItemCategory.Weapon && x.Item.Type == (int)ItemType.TwoHandedBow);
                    if (rangedWeaponToEquip.IsNotNull())
                        EquipItem(rangedWeaponToEquip);

                    var magicWeaponToEquip = inventoryItems.FirstOrDefault(x => x.Item.Category == (int)ItemCategory.Weapon && x.Item.Type == (int)ItemType.TwoHandedStaff);
                    if (magicWeaponToEquip.IsNotNull())
                        EquipItem(magicWeaponToEquip);

                    ReadOnlyInventoryItem equippedPet;
                    if (equippedPetInventoryItem.IsNotNull())
                    {
                        equippedPet = GetUnequippedItem(equippedPetInventoryItem.ItemId);
                        if (equippedPet.IsNotNull())
                        {
                            EquipItem(equippedPet);
                        }
                    }
                    else
                    {
                        var petToEquip = GetUnequippedItem(ItemCategory.Pet);
                        if (petToEquip.IsNotNull())
                        {
                            EquipItem(petToEquip);
                        }
                    }

                    foreach (var itemGroup in inventoryItems
                        .Where(x =>
                            x.Item.Category != (int)ItemCategory.Weapon &&
                            x.Item.Category != (int)ItemCategory.Pet &&
                            x.Item.Category != (int)ItemCategory.StreamerToken &&
                            x.Item.Category != (int)ItemCategory.Scroll)
                        .GroupBy(x => x.Item.Type))
                    {
                        var itemToEquip = itemGroup
                            .OrderByDescending(x => GetItemValue(x.Item))
                            .FirstOrDefault();

                        if (itemToEquip.IsNotNull())
                        {
                            EquipItem(itemToEquip);
                        }
                    }
                }
            }
            finally
            {
                ValidateInventory();
            }
        }

        internal bool ContainsItem(Guid itemId, int minAmount = 1)
        {
            lock (mutex)
            {
                if (minAmount > 1)
                {
                    var count = 0L;
                    foreach (var i in GetAllItems())
                    {
                        if (i.ItemId == itemId) count += i.Amount;
                    }

                    return count >= minAmount;
                }

                foreach (var i in GetAllItems())
                {
                    if (i.ItemId == itemId)
                        return true;
                }
            }
            return false;
        }

        internal void AddPatreonTierRewards(int? tierReward = null)
        {
            try
            {
                lock (mutex)
                {
                    var character = gameData.GetCharacter(characterId);
                    if (character == null || character.CharacterIndex > 0) return;
                    var user = gameData.GetUser(character.UserId);
                    if (user == null) return;

                    var lastReward = user.LastReward.GetValueOrDefault();
                    var elapsedSinceLastReward = DateTime.UtcNow - lastReward;
                    // in case you update your tier, we also give you the reward right away.
                    if (elapsedSinceLastReward < PatreonRewardFrequency && tierReward <= user.PatreonTier)
                        return;

                    var tier = tierReward ?? user.PatreonTier ?? 0;
                    if (tier >= 3)
                    {
                        var expScroll = Guid.Parse("DA0179BE-2EF0-412D-8E18-D0EE5A9510C7");
                        // tier 3 (dragon)  = 2 exp scroll per day
                        // tier 4 (abraxas) = 4 exp scrolls per day
                        // tier 5 (phantom) = 6 exp scrolls per day
                        var scrollAmount = (tier - 2) * 2;
                        AddItem(expScroll, scrollAmount, soulbound: true);
                    }

                    if (tier >= 2)
                    {
                        var dungeonScroll = Guid.Parse("C95AC1D6-108E-4B2F-9DB2-2EF00C092BFE");
                        // tier 2 (rune)    = 2 dungeon scrolls per day
                        // tier 3 (dragon)  = 4 dungeon scrolls per day
                        // tier 4 (abraxas) = 6 dungeon scrolls per day
                        // tier 5 (phantom) = 8 dungeon scrolls per day
                        var scrollAmount = 2 + ((tier - 2) * 2);
                        AddItem(dungeonScroll, scrollAmount, soulbound: true);
                    }

                    if (tier >= 1)
                    {
                        var raidScroll = Guid.Parse("061BAA06-5B73-4BBB-A9E1-AEA4907CD309");
                        // tier 1 (mithril) = 2 raid scrolls per day
                        // tier 2 (rune)    = 4 raid scrolls per day
                        // tier 3 (dragon)  = 6 raid scrolls per day
                        // tier 4 (abraxas) = 8 raid scrolls per day
                        // tier 5 (phantom) = 10 raid scrolls per day
                        var scrollAmount = tier * 2;
                        AddItem(raidScroll, scrollAmount, soulbound: true);
                        user.LastReward = DateTime.UtcNow;
                    }
                }
            }
            finally
            {
                ValidateInventory();
            }
        }

        public IReadOnlyList<ReadOnlyInventoryItem> GetAllItems()
        {
            lock (mutex)
            {
                return items.Select(x => x.AsReadOnly(gameData)).ToList();
            }
        }

        public bool EquipItem(ReadOnlyInventoryItem invItem)
        {
            lock (mutex)
            {
                var item = this.Get(invItem);
                if (item == null) return false;
                return EquipItem(item);
            }
        }

        public bool EquipItem(InventoryItem item)
        {
            try
            {
                lock (mutex)
                {
                    // No stack exists. That means we dont own that item.

                    if (items.All(x => x.Id != item.Id))
                    {
                        return false;
                    }

                    //var existingStacks = items.Where(x => CanBeStacked(x, item)).ToArray();
                    //if (existingStacks == null || existingStacks.Length == 0)
                    //{
                    //    return false;
                    //}

                    if (item.Equipped)
                    {
                        return false;
                    }

                    var dbItem = gameData.GetItem(item.ItemId);
                    if (dbItem == null) return false;

                    var character = gameData.GetCharacter(characterId);
                    var skills = gameData.GetCharacterSkills(character.SkillsId);

                    if (!CanEquipItem(dbItem, skills)) return false;

                    var equipmentSlot = ReadOnlyInventoryItem.GetEquipmentSlot((ItemType)dbItem.Type);
                    var alreadyEquippedItem = GetEquippedItem(equipmentSlot);

                    // Equip first, then unequipped any already equipped items
                    // this will allow us to update the correct stack, otherwise we may
                    // try to update the stack we currently equipping with if the item id are the same.
                    if (item.Amount == 1)
                    {
                        item.Equipped = true;
                    }
                    else
                    {
                        item.Amount--;

                        var newStack = Copy(item, 1);
                        newStack.Equipped = true;
                        this.items.Add(newStack);
                    }

                    // Finally, unequip.
                    if (alreadyEquippedItem.IsNotNull())
                    {
                        UnequipItem(alreadyEquippedItem);
                    }

                    return true;
                }
            }
            finally
            {
                ValidateInventory();
            }
        }

        public bool UnequipItem(InventoryItem item)
        {
            try
            {
                lock (mutex)
                {
                    // No stack exists. That means we dont own that item.
                    if (items.All(x => x.Id != item.Id))
                    {
                        return false;
                    }


                    if (!item.Equipped)
                    {
                        return false;
                    }

                    item.Equipped = false;

                    if (!string.IsNullOrEmpty(item.Enchantment))
                    {
                        return true;
                    }

                    var existingStacks = items.AsList(x => CanBeStacked(x, item));
                    if (existingStacks.Count > 0)
                    {
                        var otherStack = existingStacks.FirstOrDefault(x => x.Id != item.Id && !x.Equipped);
                        if (otherStack != null)
                        {
                            otherStack.Amount++;
                            RemoveStack(item);
                        }
                    }

                    return true;
                }
            }
            finally
            {
                ValidateInventory();
            }
        }

        public bool UnequipItem(ReadOnlyInventoryItem item)
        {
            lock (mutex)
            {
                if (!item.Equipped)
                    return false;

                var inventoryItem = Get(item);
                if (inventoryItem == null) return false;
                return UnequipItem(inventoryItem);
            }
        }

        public ReadOnlyInventoryItem GetEquippedItem(EquipmentSlot slot)
        {
            return GetEquippedItems().FirstOrDefault(x => x.EquipmentSlot == slot);
        }

        public ReadOnlyInventoryItem GetEquippedItem(ItemCategory itemCategory)
        {
            lock (mutex)
            {
                var item = items
                    .FirstOrDefault(x => x.Equipped && gameData.GetItem(x.ItemId)?.Category == (int)itemCategory);
                if (item != null)
                    return item.AsReadOnly(gameData);
                return default;
            }
        }

        public ReadOnlyInventoryItem GetUnequippedItem(ItemCategory itemCategory)
        {
            lock (mutex)
            {
                var item = items
                    .FirstOrDefault(x => !x.Equipped && gameData.GetItem(x.ItemId)?.Category == (int)itemCategory);
                if (item != null)
                    return item.AsReadOnly(gameData);
                return default;
            }
        }

        public IReadOnlyList<ReadOnlyInventoryItem> GetUnequippedItems(ItemCategory itemCategory)
        {
            lock (mutex)
            {
                return items.SelectWhere(
                    x => !x.Equipped && gameData.GetItem(x.ItemId)?.Category == (int)itemCategory,
                    x => x.AsReadOnly(gameData));
            }
        }

        public ReadOnlyInventoryItem GetEquippedItem(int itemCategory, int itemType)
        {
            lock (mutex)
            {
                var result = items.FirstOrDefault(x =>
                {
                    var item = gameData.GetItem(x.ItemId);
                    return x.Equipped && item.Category == itemCategory && item.Type == itemType;
                });

                if (result != null)
                    return result.AsReadOnly(gameData);

                return default;
            }
        }

        public DataModels.InventoryItem AddItemInstance(RavenNest.Models.AddItemRequest itemInstanceToCopy, long amount = 1)
        {
            try
            {
                lock (mutex)
                {
                    var i = itemInstanceToCopy;
                    // in this case, unless its Id is Empty guid, we even want to use the ID. GIven, there is no such id already present in the game.
                    InventoryItem resultStack = null;
                    if (CanBeStacked(i))
                    {
                        var existing = GetUnequipped(i);
                        if (existing != null)
                        {
                            existing.Amount += amount;
                            resultStack = existing;
                            return resultStack;
                        }
                    }
                    resultStack = Copy(i, amount);
                    this.items.Add(resultStack);
                    return resultStack;
                }
            }
            finally
            {
                ValidateInventory();
            }
        }
        public DataModels.InventoryItem AddItemInstance(RavenNest.Models.InventoryItem itemInstanceToCopy, long amount = 1)
        {
            try
            {
                lock (mutex)
                {
                    var i = itemInstanceToCopy;
                    // in this case, unless its Id is Empty guid, we even want to use the ID. GIven, there is no such id already present in the game.
                    InventoryItem resultStack = null;
                    if (CanBeStacked(i))
                    {
                        var existing = GetUnequipped(i);
                        if (existing != null)
                        {
                            existing.Amount += amount;
                            resultStack = existing;
                            return resultStack;
                        }
                    }
                    resultStack = Copy(i, amount);
                    this.items.Add(resultStack);
                    return resultStack;
                }
            }
            finally
            {
                ValidateInventory();
            }
        }

        public DataModels.InventoryItem AddItem(DataModels.UserBankItem itemToCopy, long amount)
        {
            lock (mutex)
            {
                InventoryItem resultStack = null;
                if (CanBeStacked(itemToCopy))
                {
                    var existing = GetUnequipped(itemToCopy);
                    if (existing != null)
                    {
                        existing.Amount += amount;
                        resultStack = existing;
                        return resultStack;
                    }
                }

                resultStack = Copy(itemToCopy, amount);
                this.items.Add(resultStack);
                return resultStack;
            }
        }

        public DataModels.InventoryItem AddItem(DataModels.InventoryItem itemToCopy, long amount)
        {
            lock (mutex)
            {
                InventoryItem resultStack = null;
                if (CanBeStacked(itemToCopy))
                {
                    var existing = GetUnequipped(itemToCopy);
                    if (existing != null)
                    {
                        existing.Amount += amount;
                        resultStack = existing;
                        return resultStack;
                    }
                }

                resultStack = Copy(itemToCopy, amount);
                this.items.Add(resultStack);
                return resultStack;
            }
        }

        public bool AddItem(ReadOnlyInventoryItem itemToCopy, long amount)
        {
            lock (mutex)
            {
                if (CanBeStacked(itemToCopy))
                {
                    var existing = GetUnequipped(itemToCopy);
                    if (existing != null)
                    {
                        existing.Amount += amount;
                        return true;
                    }
                }
                return AddItemStack(itemToCopy, amount) != null;
            }
        }

        public InventoryItem AddItemStack(ReadOnlyInventoryItem itemToCopy, long amount)
        {
            lock (mutex)
            {
                var resultStack = Copy(itemToCopy, amount);
                this.items.Add(resultStack);
                return resultStack;
            }
        }

        private InventoryItem Copy(RavenNest.Models.AddItemRequest item, long amount)
        {
            var itemToCopy = gameData.GetItem(item.ItemId);
            return new InventoryItem
            {
                Id = Guid.NewGuid(),
                CharacterId = this.characterId,
                ItemId = itemToCopy.Id,
                Name = itemToCopy.Name,
                Amount = amount,
                Soulbound = itemToCopy.Soulbound,
            };
        }
        private InventoryItem Copy(RavenNest.Models.InventoryItem itemToCopy, long amount)
        {
            return new InventoryItem
            {
                Id = Guid.NewGuid(),
                CharacterId = this.characterId,
                Enchantment = itemToCopy.Enchantment,
                Flags = itemToCopy.Flags,
                ItemId = itemToCopy.ItemId,
                Name = itemToCopy.Name,
                Amount = amount,
                Soulbound = itemToCopy.Soulbound,
                Tag = itemToCopy.Tag,
                TransmogrificationId = itemToCopy.TransmogrificationId,
            };
        }

        private InventoryItem Copy(UserBankItem itemToCopy, long amount)
        {
            return new InventoryItem
            {
                Id = Guid.NewGuid(),
                CharacterId = this.characterId,
                Enchantment = itemToCopy.Enchantment,
                Flags = itemToCopy.Flags,
                ItemId = itemToCopy.ItemId,
                Name = itemToCopy.Name,
                Amount = amount,
                Soulbound = itemToCopy.Soulbound,
                Tag = itemToCopy.Tag,
                TransmogrificationId = itemToCopy.TransmogrificationId,
            };
        }

        private InventoryItem Copy(ReadOnlyInventoryItem itemToCopy, long amount)
        {
            return new InventoryItem
            {
                Id = Guid.NewGuid(),
                CharacterId = this.characterId,
                Enchantment = itemToCopy.Enchantment,
                Flags = itemToCopy.Flags,
                ItemId = itemToCopy.ItemId,
                Name = itemToCopy.Name,
                Amount = amount,
                Soulbound = itemToCopy.Soulbound,
                Tag = itemToCopy.Tag,
                TransmogrificationId = itemToCopy.TransmogrificationId,
            };
        }
        private InventoryItem Copy(InventoryItem itemToCopy, long amount)
        {
            return new InventoryItem
            {
                Id = Guid.NewGuid(),
                CharacterId = this.characterId,
                Enchantment = itemToCopy.Enchantment,
                Flags = itemToCopy.Flags,
                ItemId = itemToCopy.ItemId,
                Name = itemToCopy.Name,
                Amount = amount,
                Soulbound = itemToCopy.Soulbound,
                Tag = itemToCopy.Tag,
                TransmogrificationId = itemToCopy.TransmogrificationId,
            };
        }

        public ReadOnlyInventoryItem AddItem(
            Guid itemId,
            long amount,
            bool soulbound)
        {
            lock (mutex)
            {
                var s = CreateInventoryItem(itemId, amount, false, null, soulbound);
                items.Add(s);
                return s.AsReadOnly(gameData);
            }
        }

        public List<InventoryItem> AddItem(
            Guid itemId,
            long amount = 1,
            bool equipped = false,
            string tag = null,
            bool soulbound = false)
        {
            var output = new List<InventoryItem>();
            lock (mutex)
            {
                var item = gameData.GetItem(itemId);

                // if the added item is to be equipped, then unequip existing item of same slot
                // then add the new item as equipped.

                if (equipped)
                {
                    var eqSlot = ReadOnlyInventoryItem.GetEquipmentSlot((ItemType)item.Type);
                    var equippedItem = GetEquippedItem(eqSlot);
                    if (equippedItem.IsNotNull())
                    {
                        UnequipItem(Get(equippedItem));
                    }

                    var invItem = CreateInventoryItem(itemId, amount, true, tag, soulbound);
                    output.Add(invItem);
                    items.Add(invItem);
                }
                else
                {
                    // if it is not going to be equipped, check if we already have a stack
                    // fill it += the amount or create a new stack.

                    var stack = GetUnequipped(itemId);//Get(itemId, false, tag);
                    if (stack != null)
                    {
                        stack.Amount += amount;
                        output.Add(stack);
                    }
                    else
                    {
                        var invItem = CreateInventoryItem(itemId, amount, false, tag, soulbound);
                        output.Add(invItem);
                        items.Add(invItem);
                    }
                }

                return output;
            }
        }

        public List<MagicItemAttribute> CreateRandomAttributes(DataModels.InventoryItem targetItem, int attributeCount)
        {
#warning Enchantment for Health has been disabled. FIX ME!
            var availableAttributes = gameData.GetItemAttributes().AsList(x => x.Name.ToUpper() != "HEALTH");
            var addedAttrId = new HashSet<Guid>();

            if (availableAttributes.Count == 0)
                return new List<MagicItemAttribute>();

            var output = new List<MagicItemAttribute>();
            var maxAttempts = availableAttributes.Count * 10;
            // make sure we dont get stuck in an infinite loop.
            attributeCount = Math.Min(attributeCount, availableAttributes.Count);
            var item = gameData.GetItem(targetItem.ItemId);
            for (var i = 0; i < attributeCount; ++i)
            {
                ItemAttribute attr = null;

                var attempt = 0;
                do
                {
                    attr = availableAttributes[random.Next(0, availableAttributes.Count)];
                    ++attempt;
                }
                while (BadEnchantment(item, attr) || addedAttrId.Contains(attr.Id) && attempt < maxAttempts);

                if (attempt >= maxAttempts)
                {
                    // we failed to add this attribute.  Maybe we dont have enough attributes available.
                    continue;
                }

                if (addedAttrId.Add(attr.Id))
                {
                    var strValue = GenerateRandomAttributeValue(attr, out var dblValue);
                    output.Add(new MagicItemAttribute
                    {
                        Attribute = attr,
                        Value = strValue,
                        DoubleValue = dblValue
                    });
                }
            }
            return output;
        }

        private bool BadEnchantment(Item item, ItemAttribute attr)
        {
            return (attr.Name == "ARMOR" && item.ArmorPower == 0) ||
                   (attr.Name == "POWER" && ((item.MagicPower + item.RangedPower + item.WeaponPower) == 0)) ||
                   (attr.Name == "AIM" && ((item.MagicAim + item.RangedAim + item.WeaponAim) == 0));
        }

        //public List<InventoryItemAttribute> CreateRandomAttributes(InventoryItem s)
        //{
        //    var item = gameData.GetItem(s.ItemId);
        //    var totReq = item.RequiredAttackLevel + item.RequiredDefenseLevel + item.RequiredMagicLevel + item.RequiredRangedLevel + item.RequiredSlayerLevel;
        //    var attrCount = (int)Math.Floor(Math.Floor(totReq / 10f) / 5);
        //    return CreateRandomAttributes(s.AsReadOnly(gameData), attrCount);
        //}

        private string GenerateRandomAttributeValue(ItemAttribute attr, out double value)
        {
            var minValue = GetValue(attr.MinValue, out var minAttrValType);
            var maxValue = GetValue(attr.MaxValue, out var maxAttrValType);
            var ran = random.NextDouble();
            // give a chance for max to hit, it might not do so otherwise as we floor the value afterwards
            if (ran > 0.985) ran = 1;
            value = Math.Floor(Math.Max(minValue, (double)ran * maxValue));
            return maxAttrValType == RavenNest.Models.AttributeValueType.Percent ? $"{value}%" : value.ToString();
        }

        public static double GetValue(string val, out RavenNest.Models.AttributeValueType valueType)
        {
            if (val.Contains(':'))
            {
                val = val.Split(':')[1];
            }

            valueType = RavenNest.Models.AttributeValueType.Number;
            val = val.Replace(',', '.');
            if (string.IsNullOrEmpty(val))
            {
                return 0d;
            }
            else
            {
                if (val.EndsWith("%"))
                {
                    if (TryParse(val.Replace("%", ""), out var value))
                    {
                        valueType = RavenNest.Models.AttributeValueType.Percent;
                        //return value / 100d;
                        return value;
                    }
                }

                TryParse(val, out var number);
                return number;
            }
        }


        private static bool TryParse(string val, out double value)
        {
            if (double.TryParse(val, out value))
                return true;

            if (double.TryParse(val.Replace(',', '.'), out value))
                return true;

            return double.TryParse(val.Replace('.', ','), out value);
        }

        //public ReadOnlyInventoryItem AddItem(ReadOnlyInventoryItem invItem, long amount, bool? equipped)
        //{
        //    lock (mutex)
        //    {
        //        var eq = equipped ?? invItem.Equipped;

        //        var stack = Get(invItem) ?? Get(invItem.ItemId, eq, invItem.Tag);
        //        var soulbound = invItem.Soulbound;

        //        //if (stack == null && !eq && noMagicAttr)
        //        //{
        //        //    stack = Get(invItem.ItemId, eq, invItem.Tag);
        //        //}

        //        if (stack != null && !eq)
        //        {
        //            stack.Amount += amount;
        //            return stack.AsReadOnly(gameData);
        //        }
        //        else
        //        {
        //            var i = CreateInventoryItem(invItem, amount, eq, soulbound);
        //            items.Add(i);
        //            stack = i;
        //        }
        //        return stack.AsReadOnly(gameData);
        //    }
        //}

        private InventoryItem CreateInventoryItem(ReadOnlyInventoryItem invItem, long amount, bool eq, bool soulbound)
        {
            return CreateInventoryItem(invItem.ItemId, amount, eq, invItem.Tag, soulbound);
        }

        public bool RemoveItem(ReadOnlyInventoryItem item, long amount, out long remainder)
        {
            return RemoveItem(Get(item), amount, out remainder);
        }

        public bool RemoveItem(ReadOnlyInventoryItem item, long amount)
        {
            return RemoveItem(Get(item), amount, out _);
        }

        public bool RemoveItem(InventoryItem stack, long amount)
        {
            return RemoveItem(stack, amount, out _);
        }
        public bool RemoveItem(InventoryItem stack, long amount, out long remainder)
        {
            lock (mutex)
            {
                remainder = 0;
                if (stack == null || stack.Amount < amount)
                {
                    if (stack.Amount <= 0)
                    {
                        //logger.LogError("Removing empty inventory stack. Character: " + this.characterId + ", ItemId: " + stack.ItemId);
                        RemoveStack(stack);
                    }

                    return false;
                }

                stack.Amount -= amount;
                if (stack.Amount <= 0)
                {
                    remainder = Math.Abs(stack.Amount.Value);
                    RemoveStack(stack);
                }

                return true;
            }
        }

        public ReadOnlyInventoryItem GetEquippedItem(Guid itemId, string tag = null)
        {
            lock (mutex)
            {
                return items
                    .FirstOrDefault(x => x.ItemId == itemId && x.Equipped == true && (x.Tag == tag || tag == null))
                    .AsReadOnly(gameData);
            }
        }

        public ReadOnlyInventoryItem GetUnequippedItem(Guid itemId, string tag = null)
        {
            lock (mutex)
            {
                return items
                    .FirstOrDefault(x => x.ItemId == itemId && x.Equipped == false && (x.Tag == tag || tag == null))
                    .AsReadOnly(gameData);
            }
        }

        internal void AddStreamerTokens(GameSession session, int amount)
        {
            lock (mutex)
            {
                var streamer = gameData.GetUser(session.UserId);
                var tokenTag = streamer.UserId;
                var item = gameData.GetItems().FirstOrDefault(x => x.Category == (int)ItemCategory.StreamerToken);
                AddItem(item.Id, amount, tag: tokenTag, soulbound: true);
            }
        }

        internal ReadOnlyInventoryItem GetByItemId(Guid itemId)
        {
            lock (mutex)
            {
                return items.FirstOrDefault(x => x.ItemId == itemId)?.AsReadOnly(gameData) ?? default;
            }
        }

        internal IReadOnlyList<ReadOnlyInventoryItem> GetStreamerTokens(GameSession session)
        {
            lock (mutex)
            {
                var streamer = gameData.GetUser(session.UserId);
                if (streamer == null) return new List<ReadOnlyInventoryItem>();
                return items.SelectWhere(
                    x => gameData.GetItem(x.ItemId).Category == (int)ItemCategory.StreamerToken && (x.Tag == streamer.UserId || x.Tag == null),
                    x => x.AsReadOnly(gameData));
            }
        }

        //public 
        public ReadOnlyInventoryItem Get(Guid inventoryItemId)
        {
            lock (mutex)
            {
                return items.FirstOrDefault(x => x.Id == inventoryItemId)?.AsReadOnly(gameData) ?? default;
            }
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private InventoryItem GetUnequipped(RavenNest.Models.AddItemRequest i)
        {
            lock (mutex) return items.FirstOrDefault(x => CanBeStacked(x, i) && !x.Equipped);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private InventoryItem GetUnequipped(RavenNest.Models.InventoryItem i)
        {
            lock (mutex) return items.FirstOrDefault(x => CanBeStacked(x, i) && !x.Equipped);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private InventoryItem GetUnequipped(DataModels.UserBankItem i)
        {
            lock (mutex) return items.FirstOrDefault(x => CanBeStacked(x, i) && !x.Equipped);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private InventoryItem GetUnequipped(DataModels.InventoryItem i)
        {
            lock (mutex) return items.FirstOrDefault(x => CanBeStacked(x, i) && !x.Equipped);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private InventoryItem GetUnequipped(ReadOnlyInventoryItem i)
        {
            lock (mutex) return items.FirstOrDefault(x => CanBeStacked(x, i) && !x.Equipped);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public InventoryItem GetUnequipped(Guid itemId)
        {
            lock (mutex)
            {
                return items.FirstOrDefault(x => CanBeStacked(x) && x.ItemId == itemId && !x.Equipped);
            }
        }

        public InventoryItem Get(Guid itemId, bool equipped, string tag)
        {
            lock (mutex)
            {
                return items.FirstOrDefault(x => x.ItemId == itemId && x.Equipped == equipped && (x.Tag == tag || tag == null));
            }
        }

        public InventoryItem Get(ReadOnlyInventoryItem item, bool lockItem = false)
        {
            lock (mutex)
            {
                var i = items.FirstOrDefault(x => x.Id == item.Id);

                if (lockItem)
                {
                    locked.Add(i.Id);
                }

                return i;
            }
        }

        public bool IsLocked(Guid inventoryItemId)
        {
            lock (mutex)
            {
                return locked.Contains(inventoryItemId);
            }
        }

        public void Unlock(Guid inventoryItemId)
        {
            lock (mutex)
            {
                locked.Remove(inventoryItemId);
            }
        }

        public ReadOnlyInventoryItem GetEquippedItem(Guid itemId)
        {
            lock (mutex)
            {
                var item = items.FirstOrDefault(x => x.Equipped && x.ItemId == itemId);
                if (item != null)
                    return item.AsReadOnly(gameData);
                return default;
            }
        }

        public IReadOnlyList<ReadOnlyInventoryItem> GetEquippedItems()
        {
            lock (mutex)
            {
                return items.SelectWhere(x => x.Equipped, x => x.AsReadOnly(gameData));
            }
        }

        public IReadOnlyList<ReadOnlyInventoryItem> GetEquippedItems(int category, int itemType)
        {
            lock (mutex)
            {
                return items.SelectWhere(x =>
                {
                    var item = gameData.GetItem(x.ItemId);
                    return x.Equipped && item.Category == category && item.Type == itemType;
                }, x => x.AsReadOnly(gameData));
            }
        }

        public IReadOnlyList<ReadOnlyInventoryItem> GetUnequippedItems()
        {
            lock (mutex)
            {
                return items.SelectWhere(x => !x.Equipped, x => x.AsReadOnly(gameData));
            }
        }


        public void RemoveStack(ReadOnlyInventoryItem stack)
        {
            RemoveStack(Get(stack));
        }

        public void RemoveStack(InventoryItem stack)
        {
            lock (mutex)
            {
                items.Remove(stack);
            }
        }

        public void RemoveStack(Guid inventoryItemId)
        {
            lock (mutex)
            {
                var stack = items.FirstOrDefault(x => x.Id == inventoryItemId);
                if (stack == null)
                {
                    return;
                }
                RemoveStack(stack);
            }
        }
        public void UnequipAllItems()
        {
            lock (mutex)
            {
                var equippedItems = items.AsList(x => x.Equipped);
                foreach (var equippedItem in equippedItems)
                {
                    UnequipItem(equippedItem);
                }
            }
        }

        private InventoryItem CreateInventoryItem(Guid itemId, long amount, bool equipped, string tag, bool soulbound)
        {
            return new InventoryItem
            {
                Id = Guid.NewGuid(),
                CharacterId = characterId,
                Amount = amount,
                ItemId = itemId,
                Equipped = equipped,
                Tag = tag,
                Soulbound = soulbound
            };
        }


        #region Helper Code

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CanEquipItem(RavenNest.Models.Item item, WebsitePlayer skills)
        {
            if (item == null)
                return false;

            return item.Category != RavenNest.Models.ItemCategory.Resource &&
                   item.Category != RavenNest.Models.ItemCategory.StreamerToken &&
                   item.Category != RavenNest.Models.ItemCategory.Scroll &&
                   item.RequiredSlayerLevel <= skills.Skills.SlayerLevel &&
                   (item.RequiredMagicLevel <= skills.Skills.MagicLevel || item.RequiredMagicLevel <= skills.Skills.HealingLevel) &&
                   item.RequiredRangedLevel <= skills.Skills.RangedLevel &&
                   item.RequiredDefenseLevel <= skills.Skills.DefenseLevel && //GameMath.ExperienceToLevel(skills.Defense) &&
                   item.RequiredAttackLevel <= skills.Skills.AttackLevel; //GameMath.ExperienceToLevel(skills.Attack);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CanEquipItem(Item item, Skills skills)
        {
            if (item == null)
                return false;

            return item.Category != (int)ItemCategory.Resource &&
                   item.Category != (int)ItemCategory.StreamerToken &&
                   item.Category != (int)ItemCategory.Scroll &&
                   item.RequiredSlayerLevel <= skills.SlayerLevel &&
                   (item.RequiredMagicLevel <= skills.MagicLevel || item.RequiredMagicLevel <= skills.HealingLevel) &&
                   item.RequiredRangedLevel <= skills.RangedLevel &&
                   item.RequiredDefenseLevel <= skills.DefenseLevel && //GameMath.ExperienceToLevel(skills.Defense) &&
                   item.RequiredAttackLevel <= skills.AttackLevel; //GameMath.ExperienceToLevel(skills.Attack);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsItemBetter(Item itemA, Item itemB)
        {
            var valueA = GetItemValue(itemA);
            var valueB = GetItemValue(itemB);
            return valueA > valueB;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetItemValue(Item item)
        {
            return item.Level + item.WeaponAim + item.WeaponPower + item.ArmorPower + item.MagicAim + item.MagicPower + item.RangedAim + item.RangedPower;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CanBeStacked(DataModels.UserBankItem a, RavenNest.Models.InventoryItem b)
        {
            return CanBeStacked(a) && CanBeStacked(b) && a.Tag == b.Tag && a.ItemId == b.ItemId;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CanBeStacked(DataModels.UserBankItem a, DataModels.InventoryItem b)
        {
            return CanBeStacked(a) && CanBeStacked(b) && a.Tag == b.Tag && a.ItemId == b.ItemId;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CanBeStacked(DataModels.InventoryItem a, RavenNest.Models.InventoryItem b)
        {
            return CanBeStacked(a) && CanBeStacked(b) && a.Tag == b.Tag && a.ItemId == b.ItemId;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CanBeStacked(DataModels.InventoryItem a, RavenNest.Models.AddItemRequest b)
        {
            return CanBeStacked(a) && CanBeStacked(b) && a.ItemId == b.ItemId;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CanBeStacked(DataModels.InventoryItem a, ReadOnlyInventoryItem b)
        {
            return CanBeStacked(a) && CanBeStacked(b) && a.Tag == b.Tag && a.ItemId == b.ItemId;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CanBeStacked(DataModels.InventoryItem a, DataModels.InventoryItem b)
        {
            return CanBeStacked(a) && CanBeStacked(b) && a.Tag == b.Tag && a.ItemId == b.ItemId;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CanBeStacked(DataModels.InventoryItem a, DataModels.UserBankItem b)
        {
            return CanBeStacked(a) && CanBeStacked(b) && a.Tag == b.Tag && a.ItemId == b.ItemId;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CanBeStacked(DataModels.UserBankItem item)
        {
            return item != null && item.TransmogrificationId == null && string.IsNullOrEmpty(item.Enchantment);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CanBeStacked(ReadOnlyInventoryItem item)
        {
            return !item.IsNull() && item.TransmogrificationId == null && string.IsNullOrEmpty(item.Enchantment);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CanBeStacked(DataModels.InventoryItem item)
        {
            return item != null && item.TransmogrificationId == null && string.IsNullOrEmpty(item.Enchantment);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CanBeStacked(RavenNest.Models.InventoryItem item)
        {
            return item != null && item.TransmogrificationId == null && string.IsNullOrEmpty(item.Enchantment);
        }

        public static bool CanBeStacked(RavenNest.Models.AddItemRequest itemAdd)
        {
            return true;
        }


        public static IReadOnlyList<RavenNest.Models.ItemEnchantment> GetItemEnchantments(string enchantmentString)
        {
            List<RavenNest.Models.ItemEnchantment> enchantments = null;

            if (enchantmentString == null)
            {
                return enchantments;
            }

            if (!string.IsNullOrEmpty(enchantmentString))
            {
                enchantments = new List<RavenNest.Models.ItemEnchantment>();
                var en = enchantmentString.Split(';');
                foreach (var e in en)
                {
                    var data = e.Split(':');
                    var key = data[0];
                    var value = GetValue(data[1], out var type);

                    //var attr = availableAttributes.FirstOrDefault(x => x.Name == key);
                    string description;

                    if (type == RavenNest.Models.AttributeValueType.Percent)
                    {
                        //if (attr != null)
                        //{
                        //    description = attr.Description.Replace(attr.MaxValue, value + "%");
                        //}

                        description = "Increases " + key.ToLower() + " by " + value + "%";
                        value = value / 100d;
                    }
                    else
                    {
                        description = "Increases " + key.ToLower() + " by " + value;
                        //if (attr != null)
                        //{
                        //    description = attr.Description.Replace(attr.MaxValue, value.ToString());
                        //}
                    }

                    enchantments.Add(new RavenNest.Models.ItemEnchantment
                    {
                        Name = (key[0] + key.ToLower().Substring(1)),
                        Value = value,
                        ValueType = type,
                        Description = description,
                    });
                }
            }
            return enchantments;
        }

        #endregion
    }

    public class MagicItemAttribute
    {
        public DataModels.ItemAttribute Attribute { get; set; }
        public string Value { get; set; }
        public double DoubleValue { get; set; }
    }


    public static class InventoryItemExtensions
    {
        public static ReadOnlyInventoryItem AsReadOnly(this InventoryItem item, GameData gameData)
        {
            if (item == null) return default;
            return ReadOnlyInventoryItem.Create(gameData, item);
        }

        public static bool IsNull(this ReadOnlyInventoryItem item)
        {
            return item.Id == Guid.Empty;
        }

        public static bool IsNotNull(this ReadOnlyInventoryItem item)
        {
            return item.Id != Guid.Empty;
        }
    }

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
    }
}

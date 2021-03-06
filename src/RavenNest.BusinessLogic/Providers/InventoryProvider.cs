﻿using RavenNest.BusinessLogic.Data;
using RavenNest.DataModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace RavenNest.BusinessLogic.Providers
{
    public class PlayerInventory
    {
        private readonly Guid characterId;
        private readonly IGameData gameData;
        private readonly object mutex = new object();
        private List<InventoryItem> items;

        private static readonly TimeSpan PatreonRewardFrequency = TimeSpan.FromDays(1);
        private static readonly Random random = new Random();
        public PlayerInventory(IGameData gameData, Guid characterId)
        {
            this.gameData = gameData;
            this.characterId = characterId;
            items = GetInventoryItems(characterId);
            //AddPatreonTierRewards();
            //MergeItems(this.items.ToList());
        }

        private List<InventoryItem> GetInventoryItems(Guid characterId)
        {
            var output = new List<InventoryItem>();
            var invItems = gameData?.GetAllPlayerItems(characterId)?.ToList() ?? new List<InventoryItem>();
            foreach (var i in invItems)
            {
                output.Add(i);
            }
            return output;
        }

        public void EquipBestItems()
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

        internal void AddPatreonTierRewards(int? tierReward = null)
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
                if (invItem.Equipped)
                {
                    return false;
                }

                var itemId = invItem.ItemId;
                var item = invItem.Item;
                if (item != null)
                {
                    var eq = GetEquippedItem(invItem.EquipmentSlot);
                    if (eq.IsNotNull())
                    {
                        UnequipItem(eq);
                    }
                }

                if (RemoveItem(invItem, 1, out var attributes))
                {
                    AddItem(invItem, 1, true);
                    return true;
                }
                return false;
            }
        }

        public ReadOnlyInventoryItem GetEquippedItem(EquipmentSlot slot)
        {
            return GetEquippedItems().FirstOrDefault(x => x.EquipmentSlot == slot);
        }

        public bool UnequipItem(ReadOnlyInventoryItem item)
        {
            lock (mutex)
            {
                if (!item.Equipped)
                {
                    return false;
                }

                if (RemoveItem(item, 1, out var attributes))
                {
                    AddItem(item, 1, false);
                    return true;
                }
                return false;
            }
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
                return items
                    .Where(x => !x.Equipped && gameData.GetItem(x.ItemId)?.Category == (int)itemCategory)
                    .Select(x => x.AsReadOnly(gameData))
                    .ToList();
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

        public void AddItem(
            Guid itemId,
            long amount = 1,
            bool equipped = false,
            string tag = null,
            bool soulbound = false,
            bool randomMagicAttributes = false,
            IReadOnlyList<InventoryItemAttribute> magicAttributes = null)
        {
            lock (mutex)
            {
                var willBeSoulbound = magicAttributes != null || randomMagicAttributes;
                if (willBeSoulbound)
                {
                    for (var i = 0; i < amount; ++i)
                    {
                        var s = CreateInventoryItem(itemId, amount, equipped, tag, willBeSoulbound);
                        var attributes = new List<InventoryItemAttribute>();
                        if (magicAttributes != null)
                        {
                            foreach (var att in magicAttributes)
                            {
                                att.InventoryItemId = s.Id;
                                gameData.Add(att);
                            }
                        }
                        else if (randomMagicAttributes)
                        {
                            attributes = CreateRandomAttributes(s);
                            foreach (var att in attributes)
                            {
                                gameData.Add(att);
                            }
                        }

                        items.Add(s);
                        gameData.Add(s);
                    }
                    return;
                }

                var stack = Get(itemId, false, tag);
                if (stack != null && !equipped)
                {
                    stack.Amount += amount;
                }
                else
                {
                    var item = CreateInventoryItem(itemId, amount, equipped, tag, soulbound);
                    items.Add(item);
                    gameData.Add(item);
                }
            }
        }

        private List<InventoryItemAttribute> CreateRandomAttributes(InventoryItem s)
        {
            var item = gameData.GetItem(s.ItemId);
            var totReq = item.RequiredAttackLevel + item.RequiredDefenseLevel + item.RequiredMagicLevel + item.RequiredRangedLevel + item.RequiredSlayerLevel;
            var attrCount = (int)Math.Floor(Math.Floor(totReq / 10f) / 5);
            var availableAttributes = gameData.GetItemAttributes();
            var addedAttrId = new HashSet<Guid>();

            if (availableAttributes.Count == 0)
                return new List<InventoryItemAttribute>();

            var output = new List<InventoryItemAttribute>();

            // make sure we dont get stuck in an infinite loop.
            attrCount = Math.Min(attrCount, availableAttributes.Count);
            for (var i = 0; i < attrCount; ++i)
            {
                ItemAttribute attr = null;
                do attr = availableAttributes[random.Next(0, availableAttributes.Count)];
                while (addedAttrId.Contains(attr.Id));
                if (addedAttrId.Add(attr.Id))
                {
                    output.Add(new InventoryItemAttribute
                    {
                        AttributeId = attr.Id,
                        Id = Guid.NewGuid(),
                        InventoryItemId = s.Id,
                        Value = GenerateRandomAttributeValue(attr)
                    });
                }
            }
            return output;
        }

        private string GenerateRandomAttributeValue(ItemAttribute attr)
        {
            var minValue = GetValue(attr.MinValue, out var minAttrValType);
            var maxValue = GetValue(attr.MinValue, out var maxAttrValType);
            var ran = random.NextDouble();
            var value = Math.Max(minValue, (decimal)ran * maxValue);
            return maxAttrValType == AttributeValueType.Percent ? $"{value}%" : value.ToString();
        }

        private decimal GetValue(string val, out AttributeValueType valueType)
        {
            valueType = AttributeValueType.Number;
            if (string.IsNullOrEmpty(val))
            {
                return 0m;
            }
            else
            {
                if (val.EndsWith("%"))
                {
                    if (decimal.TryParse(val.Replace("%", ""), out var value))
                    {
                        valueType = AttributeValueType.Percent;
                        return value / 100m;
                    }
                }

                decimal.TryParse(val, out var number);
                return number;
            }
        }

        public ReadOnlyInventoryItem AddItem(ReadOnlyInventoryItem invItem, long amount = 1, bool? equipped = null)
        {
            lock (mutex)
            {
                var eq = equipped ?? invItem.Equipped;
                var stack = Get(invItem) ?? Get(invItem.ItemId, eq, invItem.Tag);
                var magicAttributes = invItem.Attributes ?? new List<InventoryItemAttribute>();
                var soulbound = invItem.Soulbound || magicAttributes.Count > 0;
                var noMagicAttr = magicAttributes.Count == 0;

                //if (stack == null && !eq && noMagicAttr)
                //{
                //    stack = Get(invItem.ItemId, eq, invItem.Tag);
                //}

                if (stack != null && !eq && noMagicAttr)
                {
                    stack.Amount += amount;
                    return stack.AsReadOnly(gameData);
                }
                else
                {
                    var i = CreateInventoryItem(invItem, amount, eq, soulbound);
                    foreach (var attr in magicAttributes)
                    {
                        var attribute = CreateMagicAttribute(i.Id, attr);
                        gameData.Add(attribute);
                    }

                    items.Add(i);
                    gameData.Add(i);
                    stack = i;
                }
                return stack.AsReadOnly(gameData);
            }
        }

        private InventoryItem CreateInventoryItem(ReadOnlyInventoryItem invItem, long amount, bool eq, bool soulbound)
        {
            return CreateInventoryItem(invItem.ItemId, amount, eq, invItem.Tag, soulbound);
        }

        private InventoryItemAttribute CreateMagicAttribute(Guid inventoryItemId, InventoryItemAttribute attr)
        {
            return new InventoryItemAttribute
            {
                Id = Guid.NewGuid(),
                AttributeId = attr.AttributeId,
                InventoryItemId = inventoryItemId,
                Value = attr.Value
            };
        }

        public bool RemoveItem(ReadOnlyInventoryItem item, long amount)
        {
            return RemoveItem(item, amount, out _);
        }

        public bool RemoveItem(ReadOnlyInventoryItem item, long amount, out IReadOnlyList<InventoryItemAttribute> magicAttributes)
        {
            return RemoveItemStack(Get(item), amount, out magicAttributes);
        }

        private bool RemoveItemStack(
            InventoryItem stack,
            long amount,
            out IReadOnlyList<InventoryItemAttribute> magicAttributes)
        {
            magicAttributes = null;
            if (stack == null || stack.Amount < amount)
            {
                return false;
            }

            stack.Amount -= amount;
            if (stack.Amount <= 0)
            {
                RemoveStack(stack, out magicAttributes);
            }
            return true;
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

        internal IReadOnlyList<ReadOnlyInventoryItem> GetStreamerTokens(GameSession session)
        {
            lock (mutex)
            {
                var streamer = gameData.GetUser(session.UserId);
                if (streamer == null) return new List<ReadOnlyInventoryItem>();
                return items
                    .Where(x => gameData.GetItem(x.ItemId).Category == (int)ItemCategory.StreamerToken
                       && (x.Tag == streamer.UserId || x.Tag == null))
                    .Select(x => x.AsReadOnly(gameData))
                    .ToList();
            }
        }

        private InventoryItem Get(Guid itemId, bool equipped = false, string tag = null)
        {
            lock (mutex)
            {
                return items.FirstOrDefault(x => x.ItemId == itemId && x.Equipped == equipped && (x.Tag == tag || tag == null));
            }
        }

        private InventoryItem Get(ReadOnlyInventoryItem item)
        {
            lock (mutex)
            {
                return items.FirstOrDefault(x => x.Id == item.Id);
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
                return items.Where(x => x.Equipped).Select(x => x.AsReadOnly(gameData)).ToList();
            }
        }
        public IReadOnlyList<ReadOnlyInventoryItem> GetEquippedItems(int category, int itemType)
        {
            lock (mutex)
            {
                return items.Where(x =>
                {
                    var item = gameData.GetItem(x.ItemId);
                    return x.Equipped && item.Category == category && item.Type == itemType;
                }).Select(x => x.AsReadOnly(gameData)).ToList();
            }
        }

        public IReadOnlyList<ReadOnlyInventoryItem> GetUnequippedItems()
        {
            lock (mutex)
            {
                return items.Where(x => !x.Equipped).Select(x => x.AsReadOnly(gameData)).ToList();
            }
        }


        public void RemoveStack(ReadOnlyInventoryItem stack)
        {
            RemoveStack(Get(stack), out _);
        }

        public void RemoveStack(InventoryItem stack)
        {
            RemoveStack(stack, out _);
        }

        public void RemoveStack(InventoryItem stack, out IReadOnlyList<InventoryItemAttribute> magicAttributes)
        {
            lock (mutex)
            {
                magicAttributes = gameData.GetInventoryItemAttributes(stack.Id);
                if (magicAttributes != null && magicAttributes.Count > 0)
                {
                    foreach (var attribute in magicAttributes)
                    {
                        gameData.Remove(attribute);
                    }
                }

                items.Remove(stack);
                gameData.Remove(stack);
            }
        }

        public void RemoveStack(Guid inventoryItemId, out IReadOnlyList<InventoryItemAttribute> magicAttributes)
        {
            lock (mutex)
            {
                magicAttributes = null;
                var stack = items.FirstOrDefault(x => x.Id == inventoryItemId);
                if (stack == null)
                {
                    return;
                }
                RemoveStack(stack, out magicAttributes);
            }
        }
        public void UnequipAllItems()
        {
            lock (mutex)
            {
                var equippedItems = GetEquippedItems();
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CanEquipItem(Item item, Skills skills)
        {
            if (item == null)
                return false;

            return item.Category != (int)ItemCategory.Resource &&
                   item.Category != (int)ItemCategory.StreamerToken &&
                   item.Category != (int)ItemCategory.Scroll &&
                   item.RequiredMagicLevel <= skills.MagicLevel &&
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
    }

    public enum AttributeValueType : int
    {
        Number = 0,
        Percent = 1
    }

    public static class InventoryItemExtensions
    {
        public static ReadOnlyInventoryItem AsReadOnly(this InventoryItem item, IGameData gameData)
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
        public long Amount { get; }
        public bool Equipped { get; }
        public string Tag { get; }
        public bool Soulbound { get; }
        public EquipmentSlot EquipmentSlot { get; }
        public IReadOnlyList<InventoryItemAttribute> Attributes { get; }
        public Item Item { get; }
        private ReadOnlyInventoryItem(
            Guid id,
            Guid itemId,
            long amount,
            bool equipped,
            string tag,
            bool soulbound,
            Item item,
            EquipmentSlot equipmentSlot,
            IReadOnlyList<InventoryItemAttribute> attributes)
        {
            Id = id;
            ItemId = itemId;
            Amount = amount;
            Equipped = equipped;
            Item = item;
            Tag = tag;
            EquipmentSlot = equipmentSlot;
            Soulbound = soulbound;
            Attributes = attributes;
        }

        public static ReadOnlyInventoryItem Create(IGameData gameData, InventoryItem item)
        {
            var i = gameData.GetItem(item.ItemId);
            if (i == null)
                return new ReadOnlyInventoryItem();
            return new ReadOnlyInventoryItem(
                item.Id,
                item.ItemId,
                item.Amount ?? 1,
                item.Equipped,
                item.Tag,
                item.Soulbound.GetValueOrDefault(),
                i,
                GetEquipmentSlot((ItemType)i.Type),
                gameData.GetInventoryItemAttributes(item.Id) ?? new List<InventoryItemAttribute>());
        }

        public static EquipmentSlot GetEquipmentSlot(ItemType type)
        {
            switch (type)
            {
                case ItemType.Amulet: return EquipmentSlot.Amulet;
                case ItemType.Ring: return EquipmentSlot.Ring;
                case ItemType.Shield: return EquipmentSlot.Shield;
                case ItemType.Helm: return EquipmentSlot.Head;
                case ItemType.Chest: return EquipmentSlot.Chest;
                case ItemType.Gloves: return EquipmentSlot.Gloves;
                case ItemType.Leggings: return EquipmentSlot.Leggings;
                case ItemType.Boots: return EquipmentSlot.Boots;
                case ItemType.Pet: return EquipmentSlot.Pet;
                case ItemType.OneHandedAxe: return EquipmentSlot.MeleeWeapon;
                case ItemType.TwoHandedAxe: return EquipmentSlot.MeleeWeapon;
                case ItemType.OneHandedMace: return EquipmentSlot.MeleeWeapon;
                case ItemType.OneHandedSword: return EquipmentSlot.MeleeWeapon;
                case ItemType.TwoHandedSword: return EquipmentSlot.MeleeWeapon;
                case ItemType.TwoHandedStaff: return EquipmentSlot.MagicWeapon;
                case ItemType.TwoHandedBow: return EquipmentSlot.RangedWeapon;
            }

            return EquipmentSlot.None;
        }
    }
}

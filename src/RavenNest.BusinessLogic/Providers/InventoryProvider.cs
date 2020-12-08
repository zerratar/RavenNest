using RavenNest.BusinessLogic.Data;
using RavenNest.DataModels;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

public interface IPlayerInventoryProvider
{
    PlayerInventory Get(Guid characterId);
}

public class PlayerInventoryProvider : IPlayerInventoryProvider
{
    private readonly IGameData gameData;
    private readonly ConcurrentDictionary<Guid, PlayerInventory> inventories
        = new ConcurrentDictionary<Guid, PlayerInventory>();

    public PlayerInventoryProvider(IGameData gameData)
    {
        this.gameData = gameData;
    }
    public PlayerInventory Get(Guid characterId)
    {
        if (inventories.TryGetValue(characterId, out var inventory))
        {
            return inventory;
        }
        return inventories[characterId] = new PlayerInventory(gameData, characterId);
    }
}

public class PlayerInventory
{
    private readonly Guid characterId;
    private readonly IGameData gameData;
    private readonly object mutex = new object();
    private List<InventoryItem> items;

    public PlayerInventory(IGameData gameData, Guid characterId)
    {
        this.gameData = gameData;
        this.characterId = characterId;
        this.items = gameData?.GetAllPlayerItems(characterId)?.ToList() ?? new List<InventoryItem>();
        MergeItems(this.items.ToList());
        //EquipBestItems();
    }

    public void EquipBestItems()
    {
        lock (mutex)
        {
            var character = gameData.GetCharacter(characterId);
            var equippedPetInventoryItem = GetEquippedItem(ItemCategory.Pet);

            UnequipAllItems();

            var skills = gameData.GetSkills(character.SkillsId);
            var inventoryItems = GetInventoryItems()
                .Select(x => new { InventoryItem = x, Item = gameData.GetItem(x.ItemId) })
                .Where(x => CanEquipItem(x.Item, skills))
                .OrderByDescending(x => GetItemValue(x.Item))
                .ToList();

            var meleeWeaponToEquip = inventoryItems.FirstOrDefault(x => x.Item.Category == (int)ItemCategory.Weapon && (x.Item.Type == (int)ItemType.OneHandedSword || x.Item.Type == (int)ItemType.TwoHandedSword || x.Item.Type == (int)ItemType.TwoHandedAxe));
            if (meleeWeaponToEquip != null)
                EquipItem(meleeWeaponToEquip.InventoryItem.ItemId);

            var rangedWeaponToEquip = inventoryItems.FirstOrDefault(x => x.Item.Category == (int)ItemCategory.Weapon && x.Item.Type == (int)ItemType.TwoHandedBow);
            if (rangedWeaponToEquip != null)
                EquipItem(rangedWeaponToEquip.InventoryItem.ItemId);

            var magicWeaponToEquip = inventoryItems.FirstOrDefault(x => x.Item.Category == (int)ItemCategory.Weapon && x.Item.Type == (int)ItemType.TwoHandedStaff);
            if (magicWeaponToEquip != null)
                EquipItem(magicWeaponToEquip.InventoryItem.ItemId);

            ReadOnlyInventoryItem equippedPet;
            if (equippedPetInventoryItem.IsNotNull())
            {
                equippedPet = GetItem(equippedPetInventoryItem.ItemId, false);
                if (equippedPet.IsNotNull())
                {
                    EquipItem(equippedPet.ItemId);
                }
            }
            else
            {
                var petToEquip = GetInventoryItem(ItemCategory.Pet);
                if (petToEquip.IsNotNull())
                {
                    EquipItem(petToEquip.ItemId);
                }
            }

            foreach (var itemGroup in inventoryItems
                .Where(x =>
                    x.Item.Category != (int)ItemCategory.Weapon &&
                    x.Item.Category != (int)ItemCategory.Pet &&
                    x.Item.Category != (int)ItemCategory.StreamerToken)
                .GroupBy(x => x.Item.Type))
            {
                var itemToEquip = itemGroup
                    .OrderByDescending(x => GetItemValue(x.Item))
                    .FirstOrDefault();

                if (itemToEquip != null)
                {
                    EquipItem(itemToEquip.InventoryItem.ItemId);
                }
            }
        }
    }

    public IReadOnlyList<ReadOnlyInventoryItem> GetAllItems()
    {
        lock (mutex)
        {
            return this.items.Select(x => x.AsReadOnly()).ToList();
        }
    }

    public bool EquipItem(Guid itemId)
    {
        lock (mutex)
        {
            var equipped = GetEquippedItem(itemId);
            if (equipped.IsNotNull())
            {
                return true;
            }

            var item = gameData.GetItem(itemId);
            if (item != null)
            {
                var eqs = GetEquippedItems(item.Category, item.Type);
                foreach (var eq in eqs)
                {
                    UnequipItem(eq.ItemId);
                }
            }
            if (RemoveItem(itemId, 1))
            {
                AddItem(itemId, 1, true);
                return true;
            }
            return false;
        }
    }

    public bool UnequipItem(Guid itemId)
    {
        lock (mutex)
        {
            var equipped = GetEquippedItem(itemId);
            if (equipped.IsNull())
            {
                return false;
            }

            if (RemoveItem(itemId, 1, true))
            {
                AddItem(itemId, 1);
                return true;
            }
            return false;
        }
    }

    public ReadOnlyInventoryItem GetEquippedItem(ItemCategory itemCategory)
    {
        lock (mutex)
        {
            var item = this.items
                .FirstOrDefault(x => x.Equipped && gameData.GetItem(x.ItemId).Category == (int)itemCategory);
            if (item != null)
                return item.AsReadOnly();
            return default;
        }
    }

    public ReadOnlyInventoryItem GetInventoryItem(ItemCategory itemCategory)
    {
        lock (mutex)
        {
            var item = this.items
                .FirstOrDefault(x => !x.Equipped && gameData.GetItem(x.ItemId)?.Category == (int)itemCategory);
            if (item != null)
                return item.AsReadOnly();
            return default;
        }
    }
    public ReadOnlyInventoryItem GetEquippedItem(int itemCategory, int itemType)
    {
        lock (mutex)
        {
            var result = this.items.FirstOrDefault(x =>
            {
                var item = gameData.GetItem(x.ItemId);
                return x.Equipped && item.Category == itemCategory && item.Type == itemType;
            });

            if (result != null)
                return result.AsReadOnly();

            return default;
        }
    }

    public ReadOnlyInventoryItem AddItem(Guid itemId, long amount = 1, bool equipped = false, string tag = null)
    {
        lock (mutex)
        {
            var stack = Get(itemId, false, tag);
            if (stack != null && !equipped)
            {
                stack.Amount += amount;
                return stack.AsReadOnly();
            }
            else
            {
                stack = Create(itemId, amount, equipped, tag);
                items.Add(stack);
                gameData?.Add(stack);
            }
            return stack.AsReadOnly();
        }
    }

    public bool RemoveItem(Guid itemId, long amount = 1, bool equipped = false, string tag = null)
    {
        lock (mutex)
        {
            var stack = Get(itemId, equipped, tag);
            if (stack == null || stack.Amount < amount)
            {
                return false;
            }
            stack.Amount -= amount;
            if (stack.Amount == 0)
            {
                items.Remove(stack);
                gameData?.Remove(stack);
            }
            return true;
        }
    }

    public ReadOnlyInventoryItem GetItem(Guid itemId, bool equipped = false, string tag = null)
    {
        lock (mutex)
        {
            return this.items
                .FirstOrDefault(x => x.ItemId == itemId && x.Equipped == equipped && (x.Tag == tag || tag == null))
                .AsReadOnly();
        }
    }

    internal void AddStreamerTokens(GameSession session, int amount)
    {
        lock (mutex)
        {
            var streamer = gameData.GetUser(session.UserId);
            var tokenTag = streamer.UserId;
            var item = gameData.GetItems().FirstOrDefault(x => x.Category == (int)ItemCategory.StreamerToken);
            AddItem(item.Id, amount, tag: tokenTag);
        }
    }

    internal IReadOnlyList<ReadOnlyInventoryItem> GetStreamerTokens(GameSession session)
    {
        lock (mutex)
        {
            var streamer = gameData.GetUser(session.UserId);
            if (streamer == null) return new List<ReadOnlyInventoryItem>();
            return this.items
                .Where(x =>
                    gameData.GetItem(x.ItemId).Category == (int)ItemCategory.StreamerToken &&
                    (x.Tag == streamer.UserId || x.Tag == null))
                .Select(x => x.AsReadOnly())
                .ToList();
        }
    }

    private InventoryItem Get(Guid itemId, bool equipped = false, string tag = null)
    {
        lock (mutex)
        {
            return this.items.FirstOrDefault(x => x.ItemId == itemId && x.Equipped == equipped && (x.Tag == tag || tag == null));
        }
    }

    public IReadOnlyList<ReadOnlyInventoryItem> GetEquippedItems()
    {
        lock (mutex)
        {
            return this.items.Where(x => x.Equipped).Select(x => x.AsReadOnly()).ToList();
        }
    }
    public IReadOnlyList<ReadOnlyInventoryItem> GetEquippedItems(int category, int itemType)
    {
        lock (mutex)
        {
            return this.items.Where(x =>
            {
                var item = gameData.GetItem(x.ItemId);
                return x.Equipped && item.Category == category && item.Type == itemType;
            }).Select(x => x.AsReadOnly()).ToList();
        }
    }

    public IReadOnlyList<ReadOnlyInventoryItem> GetInventoryItems()
    {
        lock (mutex)
        {
            return this.items.Where(x => !x.Equipped).Select(x => x.AsReadOnly()).ToList();
        }
    }

    public ReadOnlyInventoryItem GetEquippedItem(Guid itemId)
    {
        lock (mutex)
        {
            var item = this.items.FirstOrDefault(x => x.Equipped && x.ItemId == itemId);
            if (item != null)
                return item.AsReadOnly();
            return default;
        }
    }

    public void RemoveStack(Guid inventoryItemId)
    {
        lock (mutex)
        {
            var stack = this.items.FirstOrDefault(x => x.Id == inventoryItemId);
            if (stack == null)
            {
                return;
            }

            items.Remove(stack);
            gameData?.Remove(stack);
        }
    }

    public void UnequipAllItems()
    {
        lock (mutex)
        {
            var equippedItems = GetEquippedItems();
            foreach (var equippedItem in equippedItems)
            {
                if (RemoveItem(equippedItem.ItemId, 1, true))
                {
                    AddItem(equippedItem.ItemId);
                }
            }
        }
    }

    private void MergeItems(IList<InventoryItem> items)
    {
        lock (mutex)
        {
            if (items.Count > 0)
            {
                foreach (var itemStacks in items.GroupBy(x => x.ItemId))
                {

                    var amount = itemStacks.OrderByDescending(x => x.Amount).FirstOrDefault(x => !x.Equipped)?.Amount ?? 0;
                    var itemId = itemStacks.Key;

                    var item = gameData.GetItem(itemId);
                    // do not stack streamer tokens
                    if (item != null && item.Category == (int)ItemCategory.StreamerToken)
                        continue;

                    var characterId = itemStacks.FirstOrDefault().CharacterId;

                    var equipped = itemStacks.FirstOrDefault(x => x.Equipped);
                    if (equipped != null)
                    {
                        equipped.Amount = 1;
                    }

                    var stack = amount <= 0 ? null : itemStacks.FirstOrDefault(x => !x.Equipped);
                    if (stack != null)
                    {
                        stack.Amount = amount;
                    }

                    foreach (var itemStack in itemStacks)
                    {
                        if (itemStack.Id == stack?.Id || equipped?.Id == itemStack.Id)
                            continue;

                        RemoveStack(itemStack.Id);
                    }
                }
            }
        }
    }

    private InventoryItem Create(Guid itemId, long amount, bool equipped, string tag)
    {
        return new InventoryItem
        {
            Id = Guid.NewGuid(),
            CharacterId = characterId,
            Amount = amount,
            ItemId = itemId,
            Equipped = equipped,
            Tag = tag
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CanEquipItem(Item item, Skills skills)
    {
        if (item == null)
            return false;

        return item.Category != (int)ItemCategory.Resource &&
               item.Category != (int)ItemCategory.StreamerToken &&
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

public static class InventoryItemExtensions
{
    public static ReadOnlyInventoryItem AsReadOnly(this InventoryItem item)
    {
        if (item == null) return default;
        return ReadOnlyInventoryItem.Create(item);
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

    private ReadOnlyInventoryItem(Guid id, Guid itemId, long amount, bool equipped, string tag)
    {
        Id = id;
        ItemId = itemId;
        Amount = amount;
        Equipped = equipped;
        Tag = tag;
    }

    public static ReadOnlyInventoryItem Create(InventoryItem item)
    {
        return new ReadOnlyInventoryItem(item.Id, item.ItemId, item.Amount ?? 1, item.Equipped, item.Tag);
    }
}

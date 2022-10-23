using Microsoft.AspNetCore.Http;
using RavenNest.BusinessLogic.Game;
using RavenNest.Models;
using RavenNest.Sessions;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using RavenNest.BusinessLogic.Providers;
using RavenNest.BusinessLogic.Data;
using EquipmentSlot = RavenNest.BusinessLogic.Providers.EquipmentSlot;
using RavenNest.BusinessLogic.Extended;

namespace RavenNest.Blazor.Services
{
    public class ItemService : RavenNestService
    {
        private readonly IGameData gameData;
        private readonly IItemManager itemManager;
        private readonly IReadOnlyList<DataModels.ItemAttribute> availableAttributes;

        public ItemService(
            IGameData gameData,
            IItemManager itemManager,
            IHttpContextAccessor accessor,
            ISessionInfoProvider sessionInfoProvider)
            : base(accessor, sessionInfoProvider)
        {
            this.gameData = gameData;
            this.itemManager = itemManager;
            this.availableAttributes = this.gameData.GetItemAttributes();
        }

        public IReadOnlyList<DataModels.ItemAttribute> GetItemAttributes()
        {
            return availableAttributes;
        }

        public Item GetItem(Guid itemId)
        {
            return itemManager.GetItem(itemId);
        }

        public EquipmentSlot GetItemEquipmentSlot(Guid itemId)
        {
            return itemManager.GetItemEquipmentSlot(itemId);
        }

        public ItemCollection GetItems()
        {
            return itemManager.GetAllItems();
        }

        public Task<IEnumerable<Item>> SearchAsync(string search)
        {
            return Task.Run(() =>
            {
                var items = itemManager.GetAllItems();
                return items.Where(x => x.Name.Contains(search, StringComparison.OrdinalIgnoreCase));
            });
        }
        public async Task<ItemCollection> GetItemsAsync()
        {
            return await Task.Run(() => itemManager.GetAllItems());
        }

        public async Task<bool> AddResourceDropAsync(RavenNest.DataModels.ResourceItemDrop dropToAdd)
        {
            if (dropToAdd == null || string.IsNullOrEmpty(dropToAdd.ItemName) || Guid.Empty == dropToAdd.ItemId)
            {
                return false;
            }

            await Task.Run(() => gameData.Add(dropToAdd));

            return true;
        }

        public bool RemoveResourceDrop(RavenNest.DataModels.ResourceItemDrop toDelete)
        {
            if (toDelete == null) return false;
            gameData.Remove(toDelete);
            return true;
        }

        public async Task<IReadOnlyList<RavenNest.DataModels.ResourceItemDrop>> GetResourceItemDrops()
        {
            return await Task.Run(() => gameData.GetResourceItemDrops().OrderBy(x => x.LevelRequirement).ToList());
        }

        public async Task<bool> AddOrUpdateItemAsync(Item item)
        {
            return await Task.Run(() =>
            {
                var session = GetSession();
                if (!session.Authenticated || !session.Administrator)
                    return false;


                return itemManager.Upsert(item);

                //var existing = itemManager.GetItem(item.Id);
                //if (existing != null)
                //{
                //    return itemManager.TryUpdateItem(item);
                //}
                //return itemManager.TryAddItem(item);

            }).ConfigureAwait(false);
        }

        public string GetItemTier(InventoryItem item)
        {
            var i = GetItem(item.ItemId);
            if (i == null)
                return "Unknown";
            if (i.Type == RavenNest.Models.ItemType.Pet) return "pet";
            if (i.RequiredMagicLevel == 100 || i.RequiredRangedLevel == 100 || i.RequiredAttackLevel == 100 || i.RequiredDefenseLevel == 100) return "8";
            if (i.RequiredMagicLevel >= 120 || i.RequiredRangedLevel >= 120 || i.RequiredAttackLevel >= 120 || i.RequiredDefenseLevel >= 120) return "9";
            return i.Material.ToString();
        }

        public string GetItemRequirementSkill(InventoryItem item)
        {
            var i = GetItem(item.ItemId);
            if (i == null) return "";
            if (i.RequiredAttackLevel > 0) return "Requires Attack Level";
            if (i.RequiredDefenseLevel > 0) return "Requires Defense Level";
            if (i.RequiredRangedLevel > 0) return "Requires Ranged Level";
            if (i.RequiredMagicLevel > 0) return "Requires Magic Level";
            return "";
        }

        public string GetItemRequirementLevel(InventoryItem item)
        {
            var i = GetItem(item.ItemId);
            if (i == null) return "";
            if (i.RequiredAttackLevel > 0) return i.RequiredAttackLevel.ToString();
            if (i.RequiredDefenseLevel > 0) return i.RequiredDefenseLevel.ToString();
            if (i.RequiredRangedLevel > 0) return i.RequiredRangedLevel.ToString();
            if (i.RequiredMagicLevel > 0) return i.RequiredMagicLevel.ToString();
            return "";
        }

        public IReadOnlyList<ItemEnchantmentInfo> GetItemEnchantments(InventoryItem item)
        {
            var enchantments = new List<ItemEnchantmentInfo>();
            if (!string.IsNullOrEmpty(item.Enchantment))
            {
                var en = item.Enchantment.Split(';');
                foreach (var e in en)
                {
                    var data = e.Split(':');
                    var key = data[0];
                    var value = PlayerInventory.GetValue(data[1], out var type);
                    var attr = availableAttributes.FirstOrDefault(x => x.Name == key);
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

        public IReadOnlyList<ItemStat> GetItemStats(InventoryItem item)
        {
            var stats = new List<ItemStat>();
            var i = GetItem(item.ItemId);
            if (i == null) return stats;

            int aimBonus = 0;
            int armorBonus = 0;
            int powerBonus = 0;

            if (!string.IsNullOrEmpty(item.Enchantment))
            {
                var enchantments = item.Enchantment.ToLower().Split(';');
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
    }
}

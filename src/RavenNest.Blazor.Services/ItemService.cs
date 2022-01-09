using Microsoft.AspNetCore.Http;
using RavenNest.BusinessLogic.Game;
using RavenNest.Models;
using RavenNest.Sessions;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;

namespace RavenNest.Blazor.Services
{
    public class ItemService : RavenNestService
    {
        private readonly IItemManager itemManager;
        public ItemService(IItemManager itemManager, IHttpContextAccessor accessor, ISessionInfoProvider sessionInfoProvider)
            : base(accessor, sessionInfoProvider)
        {
            this.itemManager = itemManager;
        }

        public Item GetItem(Guid itemId)
        {
            return itemManager.GetItem(itemId);
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

        public IReadOnlyList<RavenNest.Blazor.Services.ItemStat> GetItemStats(InventoryItem item)
        {
            var stats = new List<RavenNest.Blazor.Services.ItemStat>();
            var i = GetItem(item.ItemId);
            if (i == null) return stats;
            if (i.WeaponAim > 0) stats.Add(new ItemStat("Weapon Aim", i.WeaponAim));
            if (i.WeaponPower > 0) stats.Add(new ItemStat("Weapon Power", i.WeaponPower));
            if (i.RangedAim > 0) stats.Add(new ItemStat("Ranged Aim", i.RangedAim));
            if (i.RangedPower > 0) stats.Add(new ItemStat("Ranged Power", i.RangedPower));
            if (i.MagicAim > 0) stats.Add(new ItemStat("Magic Aim", i.MagicAim));
            if (i.MagicPower > 0) stats.Add(new ItemStat("Magic Power", i.MagicPower));
            if (i.ArmorPower > 0) stats.Add(new ItemStat("Armor", i.ArmorPower));
            return stats;
        }
    }

    public class ItemStat
    {
        public ItemStat() { }
        public ItemStat(string name, int value)
        {
            this.Name = name;
            this.Value = value;
        }
        public string Name { get; set; }
        public int Value { get; set; }
    }
}

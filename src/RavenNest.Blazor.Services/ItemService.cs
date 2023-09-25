using Microsoft.AspNetCore.Http;
using RavenNest.BusinessLogic.Game;
using RavenNest.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic;
using RavenNest.BusinessLogic.Game.Enchantment;
using RavenNest.BusinessLogic.Game.Processors.Tasks;
using RavenNest.BusinessLogic.Extensions;
using System.IO;

namespace RavenNest.Blazor.Services
{
    public class ItemService : RavenNestService
    {
        private readonly GameData gameData;
        private readonly ItemManager itemManager;
        private readonly IReadOnlyList<DataModels.ItemAttribute> availableAttributes;

        public ItemService(
            //Microsoft.AspNetCore.Hosting.IWebHostEnvironment Environment,
            GameData gameData,
            ItemManager itemManager,
            IHttpContextAccessor accessor,
            SessionInfoProvider sessionInfoProvider)
            : base(accessor, sessionInfoProvider)
        {
            this.gameData = gameData;
            this.itemManager = itemManager;
            this.availableAttributes = this.gameData.GetItemAttributes();
        }

        public long GetPossessionCount(Guid itemId)
        {
            var invItems = gameData.GetInventoryItemsByItemId(itemId);
            var bankItems = gameData.GetUserBankItemsByItemId(itemId);
            var vendorItem = gameData.GetVendorItemByItemId(itemId);
            var marketItems = gameData.GetMarketItems(itemId);
            return invItems.Count + bankItems.Count + (vendorItem != null ? 1 : 0) + marketItems.Count;
        }

        public string GetItemImage(Guid itemId, string itemName)
        {
            return GetItemImage(itemId.ToString(), itemName);
        }

        public string GetItemImage(string itemId, string itemName)
        {
            //Environment.WebRootPath

            var path = "/imgs/items/";
            if (!System.IO.Directory.Exists("/imgs/items/"))
                path = "wwwroot" + path;

            if (!string.IsNullOrEmpty(itemName))
            {
                var fileNamePath = Path.Combine(path, itemName.ToLower().Replace("'", "").Replace(' ', '-') + ".png");
                if (System.IO.File.Exists(fileNamePath))
                {
                    return fileNamePath.Replace("wwwroot/", "");
                }
            }

            var usingItemId = Path.Combine(path, $"{itemId}.png");
            if (System.IO.File.Exists(usingItemId))
            {
                return usingItemId.Replace("wwwroot/", "");
            }

            return string.Empty;
        }

        public string GetItemImage(Guid itemId)
        {
            var item = gameData.GetItem(itemId);
            var itemName = "";
            if (item != null)
            {
                itemName = item.Name;
            }

            return GetItemImage(itemId.ToString(), itemName);
        }

        public IReadOnlyList<RavenNest.Models.ItemRecipe> GetItemRecipesByIngredient(Guid itemId)
        {
            var recipes = new List<RavenNest.Models.ItemRecipe>();
            var ingredients = gameData.GetItemRecipeIngredientsByItem(itemId);
            foreach (var ingredient in ingredients)
            {
                var recipe = gameData.GetItemRecipe(ingredient.RecipeId);
                recipes.Add(ModelMapper.Map(gameData, recipe));
            }
            return recipes;
        }

        public RavenNest.Models.ResourceItemDrop GetResourceItemDrop(Guid itemId)
        {
            var drop = gameData.GetResourceItemDrop(itemId);
            if (drop == null) return null;
            return ModelMapper.Map(drop);
        }

        public RavenNest.Models.ItemRecipe GetItemRecipe(Guid itemId)
        {
            var recipe = gameData.GetItemRecipeByItem(itemId);
            if (recipe == null) return null;
            return ModelMapper.Map(gameData, recipe);
        }

        public async Task ClearPossessionsAsync(Guid itemId)
        {
            await Task.Run(() =>
            {
                var invItems = gameData.GetInventoryItemsByItemId(itemId);
                var bankItems = gameData.GetUserBankItemsByItemId(itemId);
                var vendorItem = gameData.GetVendorItemByItemId(itemId);
                var marketItems = gameData.GetMarketItems(itemId);
                foreach (var item in invItems)
                {
                    gameData.Remove(item);
                }
                foreach (var item in bankItems)
                {
                    gameData.Remove(item);
                }
                if (vendorItem != null)
                {
                    gameData.Remove(vendorItem);
                }
                foreach (var item in marketItems)
                {
                    gameData.Remove(item);
                }
            });
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

        public IReadOnlyList<ItemEnchantment> GetItemEnchantments(InventoryItem item)
        {
            return item.GetItemEnchantments(availableAttributes);
        }

        public async Task<IReadOnlyList<VendorItemRecord>> GetVendorItemsAsync()
        {
            return await Task.Run(() =>
            {
                List<VendorItemRecord> records = new();
                foreach (var item in gameData.GetVendorItems())
                {
                    var i = gameData.GetItem(item.ItemId);
                    records.Add(new VendorItemRecord
                    {
                        VendorItem = item,
                        Item = i,
                        BuyFromVendorPrice = GameMath.CalculateVendorBuyPrice(i, item.Stock),
                        SellToVendorPrice = GameMath.CalculateVendorSellPrice(i, item.Stock)
                    });
                }

                return records;
            });
        }

        public async Task<ItemCollection> GetItemsAsync()
        {
            return await Task.Run(() => itemManager.GetAllItems());
        }

        public void InvalidateDropHandlers()
        {
            WoodcuttingTaskProcessor.Drops.ForceReloadDrops(gameData);
            FishingTaskProcessor.Drops.ForceReloadDrops(gameData);
            MiningTaskProcessor.Drops.ForceReloadDrops(gameData);
            FarmingTaskProcessor.Drops.ForceReloadDrops(gameData);
        }

        public async Task<bool> AddResourceDropAsync(RavenNest.DataModels.ResourceItemDrop dropToAdd)
        {
            if (dropToAdd == null || string.IsNullOrEmpty(dropToAdd.ItemName) || Guid.Empty == dropToAdd.ItemId)
            {
                return false;
            }

            await Task.Run(() =>
            {
                gameData.Add(dropToAdd);
                InvalidateDropHandlers();
            });

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

        public async Task<bool> DeleteItemAsync(Guid itemId)
        {
            return await Task.Run(() =>
            {
                var session = GetSession();
                if (!session.Authenticated || !session.Administrator)
                    return false;

                return itemManager.RemoveItem(itemId);
            }).ConfigureAwait(false);
        }

        public async Task<bool> AddOrUpdateItemAsync(Item item)
        {
            return await Task.Run(() =>
            {
                var session = GetSession();
                if (!session.Authenticated || !session.Administrator)
                    return false;

                return itemManager.Upsert(item);
            }).ConfigureAwait(false);
        }

        public bool Filter(ItemFilter itemFilter, RavenNest.Models.Item item)
        {
            if (itemFilter == ItemFilter.All)
                return true;

            return GetItemFilter(item) == itemFilter;
        }

        public ItemFilter GetItemFilter(Guid itemId)
        {
            var item = GetItem(itemId);
            return GetItemFilter(item);
        }

        public ItemFilter GetItemFilter(RavenNest.Models.Item item)
        {
            //var item = ItemService.GetItem(itemId);
#warning TODO: Use GameData.GetItemFilter instead or atleast move the logic so its not repeated.
            var itemType = (ItemType)item.Type;
            if (itemType == ItemType.Coins) return ItemFilter.Resources;
            if (itemType == ItemType.Mining) return ItemFilter.Mining;
            if (itemType == ItemType.Woodcutting) return ItemFilter.Woodcutting;
            if (itemType == ItemType.Gathering) return ItemFilter.Gathering;
            if (itemType == ItemType.Fishing) return ItemFilter.Fishing;
            if (itemType == ItemType.Farming) return ItemFilter.Farming;
            if (itemType == ItemType.Crafting) return ItemFilter.Crafting;
            if (itemType == ItemType.Cooking || itemType == ItemType.Food)
                return ItemFilter.Cooking;

            if (itemType == ItemType.Alchemy || itemType == ItemType.Potion)
                return ItemFilter.Alchemy;

            if (itemType == ItemType.OneHandedSword || itemType == ItemType.TwoHandedSword)
                return ItemFilter.Swords;
            if (itemType == ItemType.TwoHandedBow) return ItemFilter.Bows;
            if (itemType == ItemType.TwoHandedStaff) return ItemFilter.Staves;
            if (itemType == ItemType.TwoHandedSpear) return ItemFilter.Spears;
            if (itemType == ItemType.OneHandedAxe || itemType == ItemType.TwoHandedAxe) return ItemFilter.Axes;
            if (itemType == ItemType.Ring || itemType == ItemType.Amulet) return ItemFilter.Accessories;
            if (itemType == ItemType.Shield) return ItemFilter.Shields;
            if (itemType == ItemType.Pet) return ItemFilter.Pets;
            if (itemType == ItemType.Scroll) return ItemFilter.Scrolls;

            if ((ItemCategory)item.Category == ItemCategory.Armor)
                return ItemFilter.Armors;

            return ItemFilter.All;
        }

        public string GetTypeName(RavenNest.Models.Item item)
        {
            var str = item.Type.ToString();
            var start = Char.ToUpper(str[0]);
            var process = str[1..];

            foreach (var x in process.Select((x, i) =>
            {
                if (Char.IsUpper(x)) return i;
                return -1;
            })
            .Reverse())
            {
                if (x != -1)
                    process = process.Insert(x, " ");
            }

            return start + process;
        }

        public string GetMaterialName(RavenNest.Models.Item item)
        {
            if (item.Material == RavenNest.Models.ItemMaterial.Abraxas)
            {
                return "Abraxas";
            }

            if (item.Type == RavenNest.Models.ItemType.None || item.Material == RavenNest.Models.ItemMaterial.None)
            {
                var itemNameMaterial = "";
                if (item.Name.Contains(' '))
                {
                    itemNameMaterial = item.Name.Split(' ')[0];
                }
                if (itemNameMaterial == "Ethereum")
                {
                    return "Ether";
                }
                if (itemNameMaterial.ToLower() == "lionite")
                {
                    return "Lionsbane";
                }
                if (itemNameMaterial.ToLower() == "gold")
                {
                    return "Gold";
                }
                if (itemNameMaterial.ToLower() == "abraxas")
                {
                    return "Abraxas";
                }

                if (!string.IsNullOrEmpty(itemNameMaterial) && item.Material == RavenNest.Models.ItemMaterial.None)
                {
                    if (Enum.TryParse<RavenNest.Models.ItemMaterial>(itemNameMaterial, true, out var res))
                    {
                        return res.ToString();
                    }
                }

                return "-";
            }

            return item.Material.ToString();
        }

        public int GetMaterialIndex(RavenNest.Models.Item item)
        {
            if (item.Type == RavenNest.Models.ItemType.None || item.Material == RavenNest.Models.ItemMaterial.None)
            {
                var itemNameMaterial = "";
                if (item.Name.Contains(' '))
                {
                    itemNameMaterial = item.Name.Split(' ')[0];
                }

                if (itemNameMaterial == "Ethereum")
                    return (int)RavenNest.Models.ItemMaterial.Ether;

                if (itemNameMaterial.ToLower() == "lionite")
                    return (int)RavenNest.Models.ItemMaterial.Lionsbane;

                if (itemNameMaterial.ToLower() == "gold") // gold share adamantite spot
                    return (int)RavenNest.Models.ItemMaterial.Adamantite;

                if (itemNameMaterial.ToLower() == "abraxas")
                    return (int)RavenNest.Models.ItemMaterial.Abraxas;

                if (!string.IsNullOrEmpty(itemNameMaterial) && item.Material == RavenNest.Models.ItemMaterial.None)
                {
                    if (Enum.TryParse<RavenNest.Models.ItemMaterial>(itemNameMaterial, true, out var res))
                    {
                        return (int)res;
                    }
                }

                return 0;
            }

            return (int)item.Material;
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
            if (i.RequiredMagicLevel > 0) return "Requires Magic or Healing Level";
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


        public IReadOnlyList<ItemStat> GetItemStats(InventoryItem item)
        {
            return item.GetItemStats(gameData);
        }
    }


    public class VendorItemRecord
    {
        public DataModels.VendorItem VendorItem { get; set; }
        public DataModels.Item Item { get; set; }
        public long BuyFromVendorPrice { get; set; }
        public long SellToVendorPrice { get; set; }
    }
}

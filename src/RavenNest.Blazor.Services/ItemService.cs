using Microsoft.AspNetCore.Http;
using RavenNest.BusinessLogic.Game;

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

using ItemFilter = RavenNest.Models.ItemFilter;

namespace RavenNest.Blazor.Services
{
    public class ItemService : RavenNestService
    {
        private readonly GameData gameData;
        private readonly ItemManager itemManager;
        private readonly MarketPriceIndex marketPrices;
        private readonly IReadOnlyList<DataModels.ItemAttribute> availableAttributes;

        public ItemService(
            //Microsoft.AspNetCore.Hosting.IWebHostEnvironment Environment,
            GameData gameData,
            ItemManager itemManager,
            MarketPriceIndex marketPrices,
            IHttpContextAccessor accessor,
            SessionInfoProvider sessionInfoProvider)
            : base(accessor, sessionInfoProvider)
        {
            this.gameData = gameData;
            this.itemManager = itemManager;
            this.marketPrices = marketPrices;
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


        public bool CanTransmogrify(RavenNest.Models.Item a, RavenNest.Models.Item b)
        {
            if (a == null || b == null)
                return false;
            if (a.Type != b.Type)
            {
                // anything worn on the head can take the look of anything else worn there: a plate helmet can look
                // like a hood, a cowl or a wizard hat (class gear, v0.9.7; before, only hats and helmets could swap)
                return IsHeadPiece(a.Type) && IsHeadPiece(b.Type);
            }

            return true;
        }

        private static bool IsHeadPiece(RavenNest.Models.ItemType type) =>
            type == RavenNest.Models.ItemType.Helmet || type == RavenNest.Models.ItemType.Hat ||
            type == RavenNest.Models.ItemType.Mask || type == RavenNest.Models.ItemType.HeadCovering;

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

        /// <summary>
        /// The picture of an item a character holds. A streamer token's tag is its streamer, so it shows that
        /// streamer's picture. Any other tag is not a streamer (a boat keeps its look in the tag): until 2026-11-21
        /// every tagged item asked for a streamer's picture, and a boat with a look got the logo shown for an
        /// unknown streamer.
        /// </summary>
        public string GetInventoryItemImage(Guid itemId, string tag)
        {
            if (!string.IsNullOrEmpty(tag))
            {
                var item = gameData.GetItem(itemId);
                if (item != null && item.Category == (int)DataModels.ItemCategory.StreamerToken)
                {
                    return $"/api/twitch/logo/{tag}";
                }
            }

            return GetItemImage(itemId);
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

        public RavenNest.Models.Item GetItem(Guid? itemId)
        {
            if (itemId == null)
            {
                return null;
            }

            return itemManager.GetItem(itemId.Value);
        }

        public RavenNest.Models.Item GetItem(Guid itemId)
        {
            return itemManager.GetItem(itemId);
        }

        public RavenNest.Models.ItemCollection GetItems()
        {
            return itemManager.GetAllItems();
        }

        public Task<IEnumerable<RavenNest.Models.Item>> SearchAsync(string search)
        {
            return Task.Run(() =>
            {
                var items = itemManager.GetAllItems();
                return items.Where(x => x.Name.Contains(search, StringComparison.OrdinalIgnoreCase));
            });
        }

        public IReadOnlyList<RavenNest.Models.ItemEnchantment> GetItemEnchantments(RavenNest.Models.InventoryItem item)
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

                    // Not offered, so the page does not advertise something PlayerManager will
                    // refuse. Soulbound items can be sold to the vendor but never bought from it,
                    // or vendoring one would be a way of handing it to somebody else.
                    if (i == null || i.Soulbound) continue;

                    // The same anchor PlayerManager charges with. If these two ever read different
                    // numbers the page quotes one price and the purchase takes another, which is
                    // the sort of thing players notice before we do.
                    // Two different things on purpose. The reading is shown whether or not the
                    // vendor is allowed to act on it; the anchor is what it is actually allowed to
                    // charge, and is zero while the switch is off.
                    var market = marketPrices.Get(item.ItemId);
                    var anchor = marketPrices.GetAnchor(item.ItemId);

                    records.Add(new VendorItemRecord
                    {
                        VendorItem = item,
                        Item = i,
                        MarketPrice = market,
                        MarketAnchorUsed = anchor,
                        BuyFromVendorPrice = GameMath.CalculateVendorBuyPrice(i, item.Stock, anchor),
                        // What the game actually pays, which is flat. The stock reduction this
                        // used to show was from a function nothing else called, so the column
                        // read "1" against items worth thousands.
                        SellToVendorPrice = i.ShopSellPrice
                    });
                }

                return records;
            });
        }

        public async Task<RavenNest.Models.ItemCollection> GetItemsAsync()
        {
            return await Task.Run(() => itemManager.GetAllItems());
        }

        public void InvalidateDropHandlers()
        {
            WoodcuttingTaskProcessor.Drops.ForceReloadDrops(gameData);
            FishingTaskProcessor.Drops.ForceReloadDrops(gameData);
            MiningTaskProcessor.Drops.ForceReloadDrops(gameData);
            FarmingTaskProcessor.Drops.ForceReloadDrops(gameData);
            FightingTaskProcessor.Drops.ForceReloadDrops(gameData); // the Slayer drops (Hide)
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

        public async Task<bool> AddOrUpdateItemAsync(RavenNest.Models.Item item)
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
            return (ItemFilter)RavenNest.Models.ItemFilterExtensions.GetItemFilter(item);
        }

        /// <summary>
        ///     Which of the six rarity bands an item falls into. See <see cref="ItemRarity"/> for
        ///     why material index is the thing being banded.
        /// </summary>
        public ItemRarityBand GetRarity(RavenNest.Models.Item item)
        {
            if (item == null)
                return ItemRarityBand.Common;

            // a Legendary (docs/legendary-design.md in the game) is the rarest there is, whatever its material
            if (RavenNest.Combat.Legendary.IsLegendary(item.Id))
                return ItemRarityBand.Mythic;

            // boats have no material, which made a Brigantine "common": a boat of one's own is rare at least, and the
            // bigger the ship the rarer
            if (item.Type == RavenNest.Models.ItemType.Boat)
            {
                switch (item.Name)
                {
                    case "Galleon": return ItemRarityBand.Legendary;
                    case "Brigantine":
                    case "Sloop": return ItemRarityBand.Epic;
                    default: return ItemRarityBand.Rare;
                }
            }

            // class gear's materials come after the Elder ones, which made an Apprentice Robe "mythic": a tier is as
            // rare as the metal of its level
            var classGearTier = RavenNest.Models.ItemLines.TierOf(item.Material);
            if (classGearTier > 0)
                return ItemRarity.FromClassGearTier(classGearTier);

            return ItemRarity.FromMaterialIndex(GetMaterialIndex(item));
        }

        public string GetRarityClass(RavenNest.Models.Item item)
        {
            return ItemRarity.CssClass(GetRarity(item));
        }

        public string GetRarityName(RavenNest.Models.Item item)
        {
            if (item != null && RavenNest.Combat.Legendary.IsLegendary(item.Id)) return "Legendary";
            return ItemRarity.Name(GetRarity(item));
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
                    // a metal by its first word only: class gear's tier names are words too (the Monk's Celestial made
                    // Celestial Logs a piece of class gear), and class gear always says its material itself
                    if (Enum.TryParse<RavenNest.Models.ItemMaterial>(itemNameMaterial, true, out var res) && !RavenNest.Models.ItemLines.IsClassGear(res))
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
                    // a metal by its first word only: class gear's tier names are words too (the Monk's Celestial made
                    // Celestial Logs a piece of class gear), and class gear always says its material itself
                    if (Enum.TryParse<RavenNest.Models.ItemMaterial>(itemNameMaterial, true, out var res) && !RavenNest.Models.ItemLines.IsClassGear(res))
                    {
                        return (int)res;
                    }
                }

                return 0;
            }

            return (int)item.Material;
        }

        public string GetItemTier(RavenNest.Models.InventoryItem item)
        {
            var i = GetItem(item.ItemId);
            if (i == null)
                return "Unknown";
            if (i.Type == RavenNest.Models.ItemType.Pet) return "pet";
            if (i.RequiredMagicLevel == 100 || i.RequiredRangedLevel == 100 || i.RequiredAttackLevel == 100 || i.RequiredDefenseLevel == 100) return "8";
            if (i.RequiredMagicLevel >= 120 || i.RequiredRangedLevel >= 120 || i.RequiredAttackLevel >= 120 || i.RequiredDefenseLevel >= 120) return "9";
            return i.Material.ToString();
        }

        public string GetItemRequirementSkill(RavenNest.Models.InventoryItem item)
        {
            var i = GetItem(item.ItemId);
            if (i == null) return "";
            if (i.RequiredAttackLevel > 0) return "Requires Attack Level";
            if (i.RequiredDefenseLevel > 0) return "Requires Defense Level";
            if (i.RequiredRangedLevel > 0) return "Requires Ranged Level";
            if (i.RequiredMagicLevel > 0) return "Requires Magic or Healing Level";
            if (RavenNest.Models.ItemLines.TamingLevelFor(i.Material, i.Level) > 0) return "Requires Taming Level";
            return "";
        }

        public string GetItemRequirementLevel(RavenNest.Models.InventoryItem item)
        {
            var i = GetItem(item.ItemId);
            if (i == null) return "";
            if (i.RequiredAttackLevel > 0) return i.RequiredAttackLevel.ToString();
            if (i.RequiredDefenseLevel > 0) return i.RequiredDefenseLevel.ToString();
            if (i.RequiredRangedLevel > 0) return i.RequiredRangedLevel.ToString();
            if (i.RequiredMagicLevel > 0) return i.RequiredMagicLevel.ToString();
            var taming = RavenNest.Models.ItemLines.TamingLevelFor(i.Material, i.Level);
            if (taming > 0) return taming.ToString();
            return "";
        }


        public IReadOnlyList<RavenNest.Models.ItemStat> GetItemStats(RavenNest.Models.InventoryItem item)
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

        /// <summary>
        ///     What players pay each other for it, when enough of them have. Null means there is not
        ///     enough trading to say, which is the normal case for most items.
        /// </summary>
        public RavenNest.BusinessLogic.Game.MarketPrice MarketPrice { get; set; }

        /// <summary>
        ///     What of that the price above was actually allowed to use. Zero while the vendor is
        ///     not following the market, even when <see cref="MarketPrice"/> has a reading.
        /// </summary>
        public long MarketAnchorUsed { get; set; }
    }
}

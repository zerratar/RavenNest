using System;
using System.Linq;
using Microsoft.Extensions.Caching.Memory;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Extensions;
using RavenNest.Models;

namespace RavenNest.BusinessLogic.Game
{
    public sealed class ItemManager
    {
        private const double ItemCacheDurationSeconds = 10 * 60;
        private readonly IMemoryCache memoryCache;
        private readonly GameData gameData;

        private DateTime lastCacheInvalidation;

        public ItemManager(
            IMemoryCache memoryCache,
            GameData gameData)
        {
            this.memoryCache = memoryCache;
            this.gameData = gameData;
        }

        public RedeemableItemCollection GetRedeemableItems()
        {
            return new RedeemableItemCollection(gameData
                .GetRedeemableItems()
                .Select(x => DataMapper.Map<RedeemableItem, DataModels.RedeemableItem>(x)));
        }

        public ItemRecipeCollection GetRecipes()
        {
            if (memoryCache.TryGetValue<ItemRecipeCollection>("GetRecipes", out var recipes))
            {
                return recipes;
            }

            var collection = new ItemRecipeCollection(gameData.GetItemRecipes().Select(x => ModelMapper.Map(gameData, x)));
            return memoryCache.Set("GetRecipes", collection, DateTime.UtcNow.AddSeconds(ItemCacheDurationSeconds));
        }

        public ItemCollection GetAllItems()
        {
            if (memoryCache.TryGetValue<ItemCollection>("GetAllItems", out var itemCollection))
            {
                return itemCollection;
            }

            return InvalidateCache();
        }

        public ItemCollection GetAllItems(DateTime timestamp)
        {
            var items = gameData.GetItems();
            var collection = new ItemCollection();
            foreach (var item in items)
            {
                if (item.Hidden || item.Modified.GetValueOrDefault() < timestamp)
                {
                    continue;
                }

                collection.Add(ModelMapper.Map(gameData, item));
            }

            if (timestamp > lastCacheInvalidation && collection.Count > 0)
            {
                // force update the cache as we have new items
                InvalidateCache();
            }

            return collection;
        }

        public Item GetItem(Guid itemId)
        {
            var dataItem = gameData.GetItem(itemId);
            return ModelMapper.Map(gameData, dataItem);
        }

        public bool Upsert(Item item)
        {
            try
            {
                DataModels.Item dataItem = GetItem(item);
                if (dataItem == null)
                {
                    AddItem(item);
                    return true;
                }

                UpdateItem(item, dataItem);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool TryAddItem(Item item)
        {
            var dataItem = gameData.GetItem(item.Id);
            if (dataItem == null)
            {
                AddItem(item);
                return true;
            }

            return false;
        }

        private void AddItem(Item item)
        {
            var entity = ModelMapper.Map(item);

            if (item.CraftingRequirements != null)
            {
                foreach (var req in item.CraftingRequirements)
                {
                    var mapped = Map(req);
                    mapped.ItemId = item.Id;
                    gameData.Add(mapped);
                }
            }

            if (!item.Craftable)
            {
                item.RequiredCookingLevel = GameMath.MaxLevel + 1;
                item.RequiredCraftingLevel = GameMath.MaxLevel + 1;
            }

            entity.Modified = DateTime.UtcNow;

            gameData.Add(entity);
            InvalidateCache();
        }

        private static DataModels.ItemCraftingRequirement Map(ItemCraftingRequirement req)
        {
            return new DataModels.ItemCraftingRequirement
            {
                Id = req.Id == Guid.Empty ? Guid.NewGuid() : req.Id,
                Amount = req.Amount,
                ItemId = req.ItemId,
                ResourceItemId = req.ResourceItemId,
            };
        }

        public bool TryUpdateItem(Item item)
        {
            DataModels.Item dataItem = GetItem(item);
            if (dataItem == null)
            {
                return false;
            }

            if (item.CraftingRequirements != null)
            {
                var updateList = item.CraftingRequirements.ToList();
                var storedRequirements = gameData.GetCraftingRequirements(item.Id);

                // check if we have matching requirement, if so, update values
                foreach (var oldRequirement in storedRequirements)
                {
                    var updated = updateList.FirstOrDefault(x => x.Id == oldRequirement.Id || x.ResourceItemId == oldRequirement.ResourceItemId);
                    if (updated != null)
                    {
                        oldRequirement.Amount = updated.Amount;
                        // since we use same resource id, remove the updated now so we don't process it again.
                        updateList.Remove(updated);
                    }
                    else
                    {
                        // we didnt have this item in the update list, we should therefor remove from existing requirements.
                        gameData.Remove(oldRequirement);
                    }
                }

                // go through the update list and add all new requirements
                foreach (var newReq in updateList)
                {
                    var mapped = Map(newReq);
                    mapped.ItemId = item.Id; // ensure item id is correct.
                    gameData.Add(mapped);
                }
            }


            UpdateItem(item, dataItem);

            return true;
        }

        private void UpdateItem(Item item, DataModels.Item dataItem)
        {
            UpdateCraftingRequirements(item, dataItem);

            if (!item.Craftable)
            {
                item.RequiredCookingLevel = GameMath.MaxLevel + 1;
                item.RequiredCraftingLevel = GameMath.MaxLevel + 1;
            }
            dataItem.Description = item.Description;

            dataItem.Level = item.Level;
            dataItem.ArmorPower = item.ArmorPower;
            dataItem.WeaponAim = item.WeaponAim;
            dataItem.WeaponPower = item.WeaponPower;
            dataItem.MagicPower = item.MagicPower;
            dataItem.MagicAim = item.MagicAim;
            dataItem.RangedPower = item.RangedPower;
            dataItem.RangedAim = item.RangedAim;

            dataItem.Category = (int)item.Category;
            dataItem.Craftable = item.Craftable;
            dataItem.FemaleModelId = item.FemaleModelId;
            dataItem.FemalePrefab = item.FemalePrefab;
            dataItem.GenericPrefab = item.GenericPrefab;
            dataItem.MaleModelId = item.MaleModelId;
            dataItem.MalePrefab = item.MalePrefab;
            dataItem.Material = (int)item.Material;
            dataItem.Name = item.Name;
            dataItem.OreCost = item.OreCost;
            dataItem.RangedAim = item.RangedAim;
            dataItem.RangedPower = item.RangedPower;
            dataItem.RequiredAttackLevel = item.RequiredAttackLevel;
            dataItem.RequiredCraftingLevel = item.RequiredCraftingLevel;
            dataItem.RequiredDefenseLevel = item.RequiredDefenseLevel;
            dataItem.RequiredMagicLevel = item.RequiredMagicLevel;
            dataItem.RequiredRangedLevel = item.RequiredRangedLevel;
            dataItem.RequiredSlayerLevel = item.RequiredSlayerLevel;
            dataItem.ShopBuyPrice = item.ShopBuyPrice;
            dataItem.ShopSellPrice = item.ShopSellPrice;
            dataItem.Soulbound = item.Soulbound;
            dataItem.Type = (int)item.Type;
            dataItem.WoodCost = item.WoodCost;
            dataItem.Modified = DateTime.UtcNow;

            InvalidateCache();
        }

        private void UpdateCraftingRequirements(Item item, DataModels.Item dataItem)
        {
            var reqs = item.CraftingRequirements;
            var existingCraftingRequirements = gameData.GetCraftingRequirements(dataItem.Id);
            if (existingCraftingRequirements != null)
            {
                foreach (var req in existingCraftingRequirements)
                {
                    if (reqs != null)
                    {
                        var newReq = reqs.FirstOrDefault(x => x.ResourceItemId == req.ResourceItemId);
                        if (newReq != null)
                        {
                            req.Amount = newReq.Amount;
                            req.ResourceItemId = newReq.ResourceItemId;
                        }
                        else
                        {
                            gameData.Remove(req);
                        }
                    }
                    else
                    {
                        gameData.Remove(req);
                    }
                }
            }

            if (item.CraftingRequirements != null)
            {
                // load it one more time
                existingCraftingRequirements = gameData.GetCraftingRequirements(dataItem.Id);
                foreach (var req in item.CraftingRequirements)
                {
                    if (existingCraftingRequirements.Any(x => x.ResourceItemId == req.ResourceItemId))
                        continue;

                    var mapped = Map(req);
                    mapped.ItemId = item.Id;
                    gameData.Add(mapped);
                }
            }
        }

        private DataModels.Item GetItem(Item item)
        {
            DataModels.Item dataItem = null;
            if (item.Id == Guid.Empty)
            {
                dataItem = gameData.GetItems().FirstOrDefault(x => x.Name.Equals(item.Name, StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                dataItem = gameData.GetItem(item.Id);
            }

            return dataItem;
        }

        public bool RemoveItem(Guid itemId)
        {
            var dataItem = gameData.GetItem(itemId);
            if (dataItem == null)
            {
                return false;
            }

            // remove all inventory items using this item
            // remove all inventory item attributes using this item
            // remove all stash using this item
            // remove all item drops using this item
            // remove all crafting requirements using this item
            // remove all market items
            // remove all vendor items
            // remove all redeemable items
            // remove all market transactions
            // remove all vendor transactions
            // remove all loyalty reward
            //gameData.Remove(dataItem);

            return false;
        }

        private ItemCollection InvalidateCache()
        {
            lastCacheInvalidation = DateTime.UtcNow;
            var items = gameData.GetItems();
            var collection = new ItemCollection();
            foreach (var item in items)
            {
                if (item.Hidden)
                {
                    continue;
                }

                collection.Add(ModelMapper.Map(gameData, item));
            }

            return memoryCache.Set("GetAllItems", collection, DateTime.UtcNow.AddSeconds(ItemCacheDurationSeconds));
        }
    }
}

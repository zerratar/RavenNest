using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Game;
using RavenNest.DataModels;

namespace RavenNest.BusinessLogic.Data
{
    public partial class GameData
    {
        /// <summary>
        ///     Items a bug made more than once under one name, to be merged back into one at startup.
        ///     "Woollys Bell": asked for as "Woolly's Bell" and renamed without the apostrophe later in the same start, so
        ///     the next start did not find it and made another (found 2026-11-21, 19 of them in the dev database).
        ///     Only names listed here are merged: two items with one name that nobody listed are reported and left alone.
        /// </summary>
        private static readonly string[] ItemsMadeMoreThanOnce =
        {
            "Woollys Bell",
        };

        /// <summary>
        ///     Merges the listed items that exist more than once into one: the one held in the most places stays (the
        ///     lowest id when none is, so the choice is the same at every start), everything that points at the others
        ///     points at it afterwards, and the others are removed. Nothing to do once there is one of each, so it
        ///     can run at every start, on any database.
        ///
        ///     Runs before the known items are looked up by name (GetKnownItems) and before MergeInventoryItems, which
        ///     joins the stacks of one owner that are the same item after this. Ids written inside text (the gems in a
        ///     socket) are not followed.
        /// </summary>
        private void MergeDuplicateItems()
        {
            var sameName = items.Entities
                .Where(x => !string.IsNullOrWhiteSpace(x.Name))
                .GroupBy(x => ItemName(x.Name).Trim(), StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .ToList();

            if (sameName.Count == 0) return;

            var toMerge = sameName.Where(g => ItemsMadeMoreThanOnce.Any(n => IsItemName(n, g.Key))).ToList();
            var unlisted = sameName.Where(g => !toMerge.Contains(g)).Select(g => g.Key + " (" + g.Count() + ")").ToList();
            if (unlisted.Count > 0)
            {
                logger.LogWarning("Items: more than one item is named {Names}. They are left alone; list the name in GameData.ItemDuplicates.cs to merge them.",
                    string.Join(", ", unlisted));
            }

            if (toMerge.Count == 0) return;

            if (typedItems != null)
            {
                logger.LogError("Items: the duplicates were not merged, the known items are looked up already and may hold one that would be removed.");
                return;
            }

            // which one stays: the one players, clans and shops hold in the most places
            var held = toMerge.SelectMany(g => g).ToDictionary(x => x.Id, _ => 0);
            void Count(Guid itemId)
            {
                if (held.TryGetValue(itemId, out var n)) held[itemId] = n + 1;
            }

            var inventory = inventoryItems.Entities;
            var stash = userBankItems.Entities;
            var clanBank = clanBankItems.Entities;
            var market = marketItems.Entities;
            var shelves = streamerShopListings.Entities;
            foreach (var x in inventory) Count(x.ItemId);
            foreach (var x in stash) Count(x.ItemId);
            foreach (var x in clanBank) Count(x.ItemId);
            foreach (var x in market) Count(x.ItemId);
            foreach (var x in shelves) Count(x.ItemId);

            var keptBy = new Dictionary<Guid, Guid>(); // an item that goes -> the one that stays
            var copies = new List<Item>();
            foreach (var group in toMerge)
            {
                var keep = group.OrderByDescending(x => held[x.Id]).ThenBy(x => x.Id).First();
                foreach (var copy in group)
                {
                    if (copy.Id == keep.Id) continue;
                    keptBy[copy.Id] = keep.Id;
                    copies.Add(copy);
                }
            }

            var moved = 0;
            var dropped = 0;

            int Move<T>(IReadOnlyList<T> rows, Func<T, Guid> get, Action<T, Guid> put)
            {
                var n = 0;
                foreach (var row in rows)
                {
                    if (!keptBy.TryGetValue(get(row), out var keep)) continue;
                    put(row, keep);
                    ++n;
                }
                return n;
            }

            int MoveOptional<T>(IReadOnlyList<T> rows, Func<T, Guid?> get, Action<T, Guid?> put)
            {
                var n = 0;
                foreach (var row in rows)
                {
                    var id = get(row);
                    if (id == null || !keptBy.TryGetValue(id.Value, out var keep)) continue;
                    put(row, keep);
                    ++n;
                }
                return n;
            }

            // What an item is (its effects, drops, recipe, requirements, redeem price) comes from the one that stays.
            // A copy's rows only move over when the kept item has none of that kind; otherwise they go with the copy.
            void KeepOrMove<T>(EntitySet<T> set, Guid copyId, Guid keepId, Action<T, Guid> put, Action<T> beforeRemove = null)
                where T : Entity<T>
            {
                var rows = set[nameof(Item), copyId];
                if (rows.Count == 0) return;
                var keptHasOwn = set[nameof(Item), keepId].Count > 0;
                foreach (var row in rows)
                {
                    if (keptHasOwn)
                    {
                        beforeRemove?.Invoke(row);
                        set.Remove(row);
                        ++dropped;
                    }
                    else
                    {
                        put(row, keepId);
                        ++moved;
                    }
                }
            }

            foreach (var pair in keptBy)
            {
                var copyId = pair.Key;
                var keepId = pair.Value;

                // the vendor has one row per item: the stock is added up
                var vendorRow = vendorItems[nameof(Item), keepId].FirstOrDefault();
                foreach (var row in vendorItems[nameof(Item), copyId])
                {
                    if (vendorRow == null)
                    {
                        row.ItemId = keepId;
                        vendorRow = row;
                        ++moved;
                    }
                    else
                    {
                        vendorRow.Stock += row.Stock;
                        vendorItems.Remove(row);
                        ++dropped;
                    }
                }

                KeepOrMove(itemStatusEffects, copyId, keepId, (x, id) => x.ItemId = id);
                KeepOrMove(itemDrops, copyId, keepId, (x, id) => x.ItemId = id);
                KeepOrMove(resourceItemDrops, copyId, keepId, (x, id) => x.ItemId = id);
                KeepOrMove(itemCraftingRequirements, copyId, keepId, (x, id) => x.ItemId = id);
                KeepOrMove(redeemableItems, copyId, keepId, (x, id) => x.ItemId = id);
                KeepOrMove(itemRecipes, copyId, keepId, (x, id) => x.ItemId = id, recipe =>
                {
                    foreach (var ingredient in itemRecipeIngredients[nameof(ItemRecipe), recipe.Id])
                    {
                        itemRecipeIngredients.Remove(ingredient);
                        ++dropped;
                    }
                });
            }

            // an enemy drops an item once
            foreach (var drop in npcItemDrops.Entities)
            {
                if (!keptBy.TryGetValue(drop.ItemId, out var keep)) continue;
                if (npcItemDrops[nameof(NPC), drop.NpcId].Any(x => x.ItemId == keep))
                {
                    npcItemDrops.Remove(drop);
                    ++dropped;
                }
                else
                {
                    drop.ItemId = keep;
                    ++moved;
                }
            }

            // what players, clans and shops hold, and the looks put on other items
            moved += Move(inventory, x => x.ItemId, (x, id) => x.ItemId = id);
            moved += MoveOptional(inventory, x => x.TransmogrificationId, (x, id) => x.TransmogrificationId = id);
            moved += Move(stash, x => x.ItemId, (x, id) => x.ItemId = id);
            moved += MoveOptional(stash, x => x.TransmogrificationId, (x, id) => x.TransmogrificationId = id);
            moved += Move(market, x => x.ItemId, (x, id) => x.ItemId = id);
            moved += MoveOptional(market, x => x.TransmogrificationId, (x, id) => x.TransmogrificationId = id);
            moved += Move(shelves, x => x.ItemId, (x, id) => x.ItemId = id);
            moved += MoveOptional(shelves, x => x.TransmogrificationId, (x, id) => x.TransmogrificationId = id);
            moved += MoveOptional(vendorItems.Entities, x => x.TransmogrificationId, (x, id) => x.TransmogrificationId = id);
            moved += MoveOptional(pets.Entities, x => x.ItemId, (x, id) => x.ItemId = id);
            moved += MoveOptional(streamerQuests.Entities, x => x.RewardItemId, (x, id) => x.RewardItemId = id);
            moved += MoveOptional(streamerQuests.Entities, x => x.RewardItemTransmogrificationId, (x, id) => x.RewardItemTransmogrificationId = id);

            // a clan's bank is not gone over by MergeInventoryItems: its stacks that became the same are joined here
            var clansToJoin = new HashSet<Guid>();
            foreach (var row in clanBank)
            {
                if (row.TransmogrificationId is Guid look && keptBy.TryGetValue(look, out var keepLook))
                {
                    row.TransmogrificationId = keepLook;
                    ++moved;
                }

                if (!keptBy.TryGetValue(row.ItemId, out var keep)) continue;
                row.ItemId = keep;
                clansToJoin.Add(row.ClanId);
                ++moved;
            }

            var kept = new HashSet<Guid>(keptBy.Values);
            foreach (var clanId in clansToJoin)
            {
                var stacks = new Dictionary<StackKey, ClanBankItem>();
                foreach (var row in clanBankItems[nameof(Clan), clanId])
                {
                    if (!kept.Contains(row.ItemId)) continue;
                    var key = row.Key();
                    if (!key.IsStackable) continue;
                    if (stacks.TryGetValue(key, out var first))
                    {
                        first.Amount += row.Amount;
                        clanBankItems.Remove(row);
                        ++dropped;
                    }
                    else
                    {
                        stacks[key] = row;
                    }
                }
            }

            // where the item is a part of something else
            moved += Move(itemRecipeIngredients.Entities, x => x.ItemId, (x, id) => x.ItemId = id);
            moved += MoveOptional(itemRecipes.Entities, x => x.FailedItemId, (x, id) => x.FailedItemId = id);
            moved += Move(itemCraftingRequirements.Entities, x => x.ResourceItemId, (x, id) => x.ResourceItemId = id);
            moved += Move(redeemableItems.Entities, x => x.CurrencyItemId, (x, id) => x.CurrencyItemId = id);

            // the history
            moved += Move(marketTransactions.Entities, x => x.ItemId, (x, id) => x.ItemId = id);
            moved += Move(vendorTransaction.Entities, x => x.ItemId, (x, id) => x.ItemId = id);
            moved += Move(streamerShopTransactions.Entities, x => x.ItemId, (x, id) => x.ItemId = id);
            moved += Move(clanBankLog.Entities, x => x.ItemId, (x, id) => x.ItemId = id);
            moved += Move(dailyAggregatedMarketplaceData.Entities, x => x.ItemId, (x, id) => x.ItemId = id);

            foreach (var copy in copies)
            {
                items.Remove(copy);
            }

            foreach (var group in toMerge)
            {
                var keepId = group.Select(x => x.Id).First(id => !keptBy.ContainsKey(id));
                logger.LogWarning("Items: {Count} copies of '{Name}' merged into {Id}, which was held in {Held} place(s).",
                    group.Count() - 1, group.Key, keepId, held[keepId]);
            }

            logger.LogWarning("Items: {Copies} duplicate item(s) removed, {Moved} reference(s) moved to the kept items, {Dropped} row(s) of the copies dropped.",
                copies.Count, moved, dropped);
        }
    }
}

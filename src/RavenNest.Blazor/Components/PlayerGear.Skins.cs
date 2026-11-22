using RavenNest.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RavenNest.Blazor.Components
{
    /// <summary>
    ///     Skins (transmogrification): a piece keeps its stats and takes the look of another item, which is used up
    ///     until the skin is removed or replaced.
    /// </summary>
    public partial class PlayerGear
    {
        /// <summary>One item that can be the skin, from the bag or from the stash.</summary>
        /// <param name="StashItem">
        ///     Set for one from the stash: the server looks for the skin in the character's own inventory, so a
        ///     stash item moves there first, and this is what it moves from.
        /// </param>
        private sealed record SkinCandidate(Item Item, long Amount, bool FromStash, UserBankItem StashItem);

        private bool skinFromStash;
        private string skinSearch = string.Empty;
        private SkinCandidate skinChoice;
        private string skinError;
        private IReadOnlyList<UserBankItem> stashItems = Array.Empty<UserBankItem>();

        /// <summary>Weapons, armor and amulets can take a skin; rings, pets and everything else cannot.</summary>
        private static bool CanSkin(Entry e)
        {
            var item = e.Item;
            if (item.Type == ItemType.Pet || item.Type == ItemType.Ring || item.Type == ItemType.StreamerToken) return false;
            return item.Category == ItemCategory.Armor || item.Category == ItemCategory.Weapon || item.Category == ItemCategory.Amulet;
        }

        private void OpenSkins(Entry e)
        {
            pickedId = e.Stack.Id;
            skinError = null;
            skinSearch = string.Empty;
            skinFromStash = false;
            skinChoice = null;

            // The stash belongs to the account rather than to this character, so it is fetched here rather than
            // coming with the character.
            stashItems = Server.GetStash() ?? Array.Empty<UserBankItem>();
            dialog = Dialog.Skin;
        }

        private void SetSkinTab(bool stash)
        {
            skinFromStash = stash;
        }

        /// <summary>
        ///     Everything the character or the account holds that could stand in for the picked piece: the same
        ///     kind of item, not the piece itself, not already its skin, and not enchanted (the enchantment would be
        ///     destroyed with the item).
        /// </summary>
        private IReadOnlyList<SkinCandidate> SkinCandidates(bool fromStash)
        {
            var result = new List<SkinCandidate>();
            var target = PickedEntry;
            if (target == null) return result;

            bool Fits(Item item) =>
                item != null
                && item.Id != target.Item.Id
                && target.Stack.TransmogrificationId != item.Id
                && ItemService.CanTransmogrify(item, target.Item);

            if (fromStash)
            {
                foreach (var stashItem in stashItems)
                {
                    if (!string.IsNullOrEmpty(stashItem.Enchantment)) continue;
                    var item = ItemService.GetItem(stashItem.ItemId);
                    if (Fits(item)) result.Add(new SkinCandidate(item, stashItem.Amount, true, stashItem));
                }
            }
            else
            {
                foreach (var stack in player.InventoryItems)
                {
                    if (!string.IsNullOrEmpty(stack.Enchantment) || stack.Equipped) continue;
                    var item = ItemService.GetItem(stack.ItemId);
                    if (Fits(item)) result.Add(new SkinCandidate(item, stack.Amount, false, null));
                }
            }

            // One row per item rather than one per stack.
            return result
                .GroupBy(x => x.Item.Id)
                .Select(g => g.First() with { Amount = g.Sum(x => x.Amount) })
                .OrderBy(x => x.Item.Name)
                .ToList();
        }

        private IReadOnlyList<SkinCandidate> VisibleSkinCandidates
        {
            get
            {
                var all = SkinCandidates(skinFromStash);
                if (string.IsNullOrWhiteSpace(skinSearch)) return all;
                var needle = skinSearch.Trim();
                return all.Where(x => (x.Item.Name ?? string.Empty).Contains(needle, StringComparison.OrdinalIgnoreCase)).ToList();
            }
        }

        private void ApplySkin()
        {
            var target = PickedEntry;
            var choice = skinChoice;
            if (target == null || choice == null) return;

            var slot = target.Slot;
            var wasWorn = target.Stack.Equipped;
            var itemId = target.Stack.ItemId;
            var name = target.Name;
            var result = Server.AddTransmogrification(player.Id, target.Stack, choice.Item.Id, choice.StashItem);
            if (result?.Player != null)
            {
                player = result.Player;
                coins = result.Coins;
                Rebuild();
            }

            if (result == null || result.Refused != null || result.Done == 0)
            {
                skinError = result?.Refused ?? (choice.FromStash
                    ? "The skin could not be used. If it did not leave the stash, nothing was changed."
                    : "The skin could not be used, so nothing was changed.");
                return;
            }

            CloseDialog();
            FollowPiece(slot, wasWorn, itemId);
            Say(name + " now looks like " + choice.Item.Name + ".");
        }

        private void RemoveSkin(Entry e)
        {
            var slot = e.Slot;
            var wasWorn = e.Stack.Equipped;
            var itemId = e.Stack.ItemId;
            var name = e.Name;
            if (!Take(Server.RemoveTransmogrification(player.Id, e.Stack), "The skin was not removed. Try again in a moment.")) return;
            FollowPiece(slot, wasWorn, itemId);
            Say(name + " has its own look again, and the skin is back in the bag.");
        }

        // A skin can split one piece off a stack or join it with others, so the picked piece is looked up again.
        private void FollowPiece(RavenNest.BusinessLogic.Game.EquipmentSlot slot, bool wasWorn, Guid itemId)
        {
            if (PickedEntry != null) return;
            var again = wasWorn && worn.TryGetValue(slot, out var w) ? w : entries.FirstOrDefault(x => x.Stack.ItemId == itemId && !x.Stack.Equipped);
            pickedId = again?.Stack.Id;
        }
    }
}

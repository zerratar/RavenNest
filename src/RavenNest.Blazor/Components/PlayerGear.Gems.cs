using RavenNest.Blazor.Services;
using RavenNest.Models;
using System;
using System.Collections.Generic;
using System.Linq;

using GemKind = RavenNest.Combat.GemKind;
using GemTier = RavenNest.Combat.GemTier;
using Gems = RavenNest.Combat.Gems;

namespace RavenNest.Blazor.Components
{
    /// <summary>
    ///     Gems and sockets of the picked item (the game's docs/gems-design.md). The picker (GemPicker.razor) makes no
    ///     calls: the handlers here do, and hand it what to show.
    /// </summary>
    public partial class PlayerGear
    {
        /// <summary>
        ///     What the confirm click does, and the gem it puts at risk, as the server named it. The gem goes back
        ///     with the confirm, so it is only done while that gem is still the one in the socket.
        /// </summary>
        private sealed record GemConfirm(bool Socketing, int Socket, int Kind, int Tier, Guid OldGem, string OldName, double KeepChance);

        // The socket of the picked item that the gem picker is open for (from 1); 0 while it is closed.
        private int gemPickerSocket;
        private double gemKeepChance;
        private GemConfirm gemConfirm;
        // What the last change did, and why the last try was not done. Both are shown inside the picker.
        private GemPicker.ResultInfo gemResult;
        private string gemError;

        private void ResetGemState()
        {
            gemPickerSocket = 0;
            gemConfirm = null;
            gemResult = null;
            gemError = null;
        }

        private void OpenGemPicker(int socket)
        {
            if (!CanSocket || PickedEntry == null || socket <= 0) return;

            ResetGemState();
            gemKeepChance = Server.GetGemKeepChance(player.Id);
            gemPickerSocket = socket;
        }

        private void CloseGemPicker()
        {
            ResetGemState();
        }

        // From the result back to the list, for the same socket.
        private void BackToGemList()
        {
            gemResult = null;
            gemError = null;
        }

        private GemPicker.Gem PickerGem(GemKind kind, GemTier tier, long amount = 0)
        {
            return new GemPicker.Gem(kind, tier, amount, ItemService.GetItemImage(Gems.ItemId(kind, tier)));
        }

        // The server's question in the picker's terms; null while there is none.
        private GemPicker.ConfirmInfo PickerConfirm()
        {
            var confirm = gemConfirm;
            if (confirm == null || !Gems.TryGetGem(confirm.OldGem, out var oldKind, out var oldTier)) return null;

            return new GemPicker.ConfirmInfo(
                confirm.Socketing,
                PickerGem(oldKind, oldTier),
                confirm.Socketing ? PickerGem((GemKind)confirm.Kind, (GemTier)confirm.Tier) : null,
                confirm.KeepChance);
        }

        // The same gems the server takes from: this character's own stacks, not equipped, added up per kind and
        // tier. Best tier first per kind.
        private List<GemPicker.Gem> OwnedGems()
        {
            var amounts = new Dictionary<(GemKind Kind, GemTier Tier), long>();
            foreach (var stack in player.InventoryItems)
            {
                if (stack.Equipped || stack.Amount <= 0) continue;
                if (Gems.TryGetGem(stack.ItemId, out var kind, out var tier))
                {
                    amounts[(kind, tier)] = amounts.GetValueOrDefault((kind, tier)) + stack.Amount;
                }
            }

            return amounts
                .OrderBy(x => x.Key.Kind)
                .ThenByDescending(x => x.Key.Tier)
                .Select(x => PickerGem(x.Key.Kind, x.Key.Tier, x.Value))
                .ToList();
        }

        private static string Share(double chance)
        {
            return Math.Round(chance * 100).ToString(System.Globalization.CultureInfo.InvariantCulture) + "%";
        }

        // The picked gem into the socket the picker is open for. Always a named socket: the server takes "no
        // socket" as the first empty one. When the socket holds a gem this only asks (NeedsConfirm).
        private void SocketGem(GemPicker.Gem gem)
        {
            var e = PickedEntry;
            if (e == null || gem == null || gemPickerSocket <= 0) return;

            var change = Server.SocketGem(player.Id, e.Stack.Id, (int)gem.Kind, (int)gem.Tier, gemPickerSocket, null);
            ApplyGemChange(change, true, (int)gem.Kind, (int)gem.Tier);
        }

        // Never done by this call: the server answers with the gem and the chance, and the confirm click does it.
        private void TakeOutGem()
        {
            var e = PickedEntry;
            if (e == null || gemPickerSocket <= 0) return;

            var change = Server.UnsocketGem(player.Id, e.Stack.Id, gemPickerSocket, null);
            ApplyGemChange(change, false, 0, 0);
        }

        private void ConfirmGemChange()
        {
            var confirm = gemConfirm;
            var e = PickedEntry;
            gemConfirm = null;
            if (confirm == null || e == null) return;

            var change = confirm.Socketing
                ? Server.SocketGem(player.Id, e.Stack.Id, confirm.Kind, confirm.Tier, confirm.Socket, confirm.OldGem)
                : Server.UnsocketGem(player.Id, e.Stack.Id, confirm.Socket, confirm.OldGem);
            ApplyGemChange(change, confirm.Socketing, confirm.Kind, confirm.Tier);
        }

        private void CancelGemChange()
        {
            gemConfirm = null;
            gemError = null;
        }

        /// <summary>
        ///     Shows what a gem action did: the new state of the character and the item, then the outcome or the
        ///     reason it was not done.
        /// </summary>
        private void ApplyGemChange(GemChange change, bool socketing, int kind, int tier)
        {
            const string failed = "That did not work. Try again in a moment.";

            gemConfirm = null;
            gemResult = null;
            gemError = null;
            if (change == null || (change.Refused == null && change.Result == null))
            {
                gemError = failed;
                return;
            }

            if (change.Refused != null)
            {
                gemError = change.Refused;
                return;
            }

            // The character as the server has it now, whatever the answer. A refusal often means this page was
            // behind: the gem was used, or the item changed, in the game.
            if (change.Player != null)
            {
                // the picker stays open over the same socket while the character is read again
                var socket = gemPickerSocket;
                player = change.Player;
                coins = Server.GetCoins(player);
                pickedId = change.ItemId;
                Rebuild();
                if (PickedEntry == null)
                {
                    // The item itself is gone (sold, sent or traded since the panel opened): nothing left to show.
                    ClearPick();
                    return;
                }

                gemPickerSocket = socket;
            }

            var result = change.Result;
            switch (result.Status)
            {
                case GemResultStatus.Ok:
                    var lost = result.OldKind.HasValue && !result.Kept;
                    var outcome = OldGemOutcome(result);
                    var done = socketing
                        ? (Gems.Name((GemKind)result.Kind, (GemTier)result.Tier) + " is now in socket " + result.Socket + ". " + outcome).TrimEnd()
                        : (outcome.Length > 0 ? outcome : "The gem was taken out.");
                    // A crumbled gem is a loss even though the action was done, so it does not read as good news.
                    gemResult = new GemPicker.ResultInfo(done, lost);
                    break;

                case GemResultStatus.NeedsConfirm:
                    if (!result.OldKind.HasValue)
                    {
                        gemError = failed;
                        break;
                    }

                    var oldKind = (GemKind)result.OldKind.Value;
                    var oldTier = (GemTier)result.OldTier.GetValueOrDefault();
                    gemConfirm = new GemConfirm(socketing, result.Socket, kind, tier, Gems.ItemId(oldKind, oldTier), Gems.Name(oldKind, oldTier), result.KeepChance);
                    break;

                case GemResultStatus.NoGem:
                    gemError = "This character has no " + Gems.Name((GemKind)kind, (GemTier)tier) + " left to put in.";
                    break;

                case GemResultStatus.NoSuchItem:
                    gemError = "That item is no longer in this inventory.";
                    break;

                case GemResultStatus.Busy:
                    gemError = "That item is busy (being enchanted or traded). Try again in a moment.";
                    break;

                case GemResultStatus.NotSocketable:
                case GemResultStatus.NoSockets:
                    gemError = "This item has no sockets. Weapons, armor (not hats or masks), rings and amulets have them from item level 25.";
                    break;

                case GemResultStatus.SocketsFull:
                    gemError = "Every socket is filled. Pick the socket whose gem to replace.";
                    break;

                case GemResultStatus.EmptySocket:
                    gemError = "That socket is empty now.";
                    break;

                case GemResultStatus.NoSuchSocket:
                    gemError = "This item has " + result.SocketCount + (result.SocketCount == 1 ? " socket." : " sockets.");
                    break;

                default:
                    gemError = failed;
                    break;
            }
        }

        // "The Chipped Ruby came out whole and is back in the inventory (12% chance)." or
        // "The Chipped Ruby crumbled to dust (12% chance to keep it)." Empty when no gem was taken out.
        private static string OldGemOutcome(GemResult result)
        {
            if (!result.OldKind.HasValue) return "";

            var name = Gems.Name((GemKind)result.OldKind.Value, (GemTier)result.OldTier.GetValueOrDefault());
            return result.Kept
                ? "The " + name + " came out whole and is back in the inventory (" + Share(result.KeepChance) + " chance)."
                : "The " + name + " crumbled to dust (" + Share(result.KeepChance) + " chance to keep it).";
        }
    }
}

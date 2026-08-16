using Microsoft.AspNetCore.Components;
using RavenNest.Blazor.Services;
using RavenNest.BusinessLogic.Game;
using RavenNest.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RavenNest.Blazor.Pages.Front
{
    public partial class Marketplace
    {
        private SessionInfo session;
        private RavenNest.Models.MarketItemCollection items;
        private List<MarketItem> filtered = new();
        private bool isAdmin;
        private bool canCancelItems;

        private string sort = "name";
        private SortDirection sortDirection;
        private ItemFilter itemFilter = ItemFilter.All;
        private string search = "";

        private MarketItem listingToCancel;

        /// <summary>
        ///     Every listing of the same thing, so a row can say whether it is the best offer on the
        ///     board. Keyed on the item and its enchantment together: an enchanted sword is a
        ///     different product from a plain one and is priced like one, so comparing the two
        ///     would answer the wrong question.
        /// </summary>
        private Dictionary<string, List<MarketItem>> offersByItem = new();

        private int DistinctItemCount => offersByItem.Count;

        private double TotalAsked => items == null ? 0 : items.Sum(x => x.PricePerItem * x.Amount);

        /// <summary>The exact figure, always, for the tooltip.</summary>
        private string AskedExact => ((long)TotalAsked).ToString("N0");

        /// <summary>
        ///     True once the whole number is long enough to want the smaller of the two stat
        ///     sizes. Measured in place: thirteen digits with separators are 210px at the display
        ///     size, which is as much as a stat column has to give on a normal screen.
        /// </summary>
        private bool AskedNeedsSmallerType => AskedExact.Length > 13;

        protected override async Task OnInitializedAsync()
        {
            session = AuthService.GetSession();
            isAdmin = session != null && session.Administrator;

            // Everything is loaded once and filtered here rather than refetched per category. The
            // page already asked the server for the whole board on first load, and searching plus
            // filtering at the same time is not something the server side filter can express.
            items = await MarketplaceService.GetMarketItemsAsync();
            canCancelItems = isAdmin || items.Any(CanCancelItem);
            Reindex();
        }

        private void Reindex()
        {
            offersByItem = items
                .GroupBy(OfferKey)
                .ToDictionary(g => g.Key, g => g.ToList());

            ApplyFilters();
        }

        private static string OfferKey(MarketItem item)
        {
            return item.ItemId + "|" + (item.Enchantment ?? "");
        }

        private List<MarketItem> OffersFor(MarketItem entry)
        {
            return offersByItem.TryGetValue(OfferKey(entry), out var list) ? list : new List<MarketItem> { entry };
        }

        private static bool IsCheapest(MarketItem entry, List<MarketItem> offers)
        {
            return entry.PricePerItem <= offers.Min(x => x.PricePerItem);
        }

        private static string CheapestNote(List<MarketItem> offers)
        {
            return "The cheapest listing of this item asks "
                + offers.Min(x => x.PricePerItem).ToString("N0") + " each";
        }

        private static string OverCheapestNote(MarketItem entry, List<MarketItem> offers)
        {
            var cheapest = offers.Min(x => x.PricePerItem);
            if (cheapest <= 0)
            {
                return null;
            }

            var ratio = entry.PricePerItem / cheapest;
            if (ratio >= 10)
            {
                return Math.Round(ratio) + "x the cheapest";
            }

            return "+" + Math.Round((ratio - 1) * 100) + "% over cheapest";
        }

        /// <summary>
        ///     What a vendor pays for the item is the floor under every price on this page, and it
        ///     was the one number that made an asking price mean anything. The sort for it already
        ///     existed; the column had been commented out of the markup.
        /// </summary>
        private static string VendorNote(RavenNest.Models.Item item, double price)
        {
            if (item == null || item.ShopSellPrice <= 0)
            {
                return null;
            }

            if (price < item.ShopSellPrice)
            {
                return "under vendor value";
            }

            var ratio = price / item.ShopSellPrice;
            if (ratio >= 100)
            {
                return "over 100x vendor value";
            }

            return Math.Round(ratio, ratio < 10 ? 1 : 0) + "x vendor value";
        }

        private static double LotTotal(MarketItem entry)
        {
            return entry.PricePerItem * entry.Amount;
        }

        private static string ListedNote(MarketItem entry)
        {
            if (entry.Created == null)
            {
                return null;
            }

            return "Listed " + Utility.FormatTime(DateTime.UtcNow - entry.Created.Value) + " ago";
        }

        private void SelectItemFilter(ItemFilter newItemFilter)
        {
            itemFilter = newItemFilter;
            ApplyFilters();
            InvokeAsync(StateHasChanged);
        }

        private void OnSearchChanged(ChangeEventArgs e)
        {
            search = e.Value?.ToString() ?? "";
            ApplyFilters();
        }

        private void ClearFilters()
        {
            search = "";
            itemFilter = ItemFilter.All;
            ApplyFilters();
        }

        private void ApplyFilters()
        {
            filtered = items.Where(Filter).ToList();
        }

        private bool Filter(MarketItem entry)
        {
            if (itemFilter != ItemFilter.All && ItemService.GetItemFilter(entry.ItemId) != itemFilter)
                return false;

            if (string.IsNullOrWhiteSpace(search))
                return true;

            var name = entry.Name ?? GetItem(entry.ItemId)?.Name;
            return name != null && name.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private bool CanCancelItem(RavenNest.Models.MarketItem listedItem)
        {
            if (isAdmin) return true;
            if (session == null || listedItem == null)
                return false;
            return session.UserId == listedItem.SellerUserId;
        }

        private RavenNest.Models.Item GetItem(Guid itemId)
        {
            return ItemService.GetItem(itemId);
        }

        private string GetUserName(Guid userId)
        {
            if (!isAdmin) return null;
            return UserService.GetUser(userId)?.UserName;
        }

        private void ConfirmCancel(MarketItem entry)
        {
            listingToCancel = entry;
        }

        private void HideCancelConfirm()
        {
            listingToCancel = null;
        }

        private async Task CancelListing()
        {
            var entry = listingToCancel;
            listingToCancel = null;

            if (entry == null || !CanCancelItem(entry))
            {
                return;
            }

            if (await MarketplaceService.CancelListingAsync(entry.Id))
            {
                var itemToRemove = items.FirstOrDefault(x => x.Id == entry.Id);
                if (itemToRemove != null)
                {
                    items.Remove(itemToRemove);
                }
                else
                {
                    items = await MarketplaceService.GetMarketItemsAsync();
                }

                Reindex();
            }

            await InvokeAsync(StateHasChanged);
        }

        private MarkupString Indicator(string value)
        {
            if (value != sort)
            {
                return (MarkupString)"";
            }

            return (MarkupString)(sortDirection == SortDirection.Ascending ? "&uarr;" : "&darr;");
        }

        private string AriaSort(string value)
        {
            if (value != sort) return "none";
            return sortDirection == SortDirection.Ascending ? "ascending" : "descending";
        }

        private async void SortByName()
        {
            SetSorting("name");
            await SortBy((m, x) => m.Name ?? x.Name);
        }

        private async void SortByLevelReq()
        {
            SetSorting("lev-req");
            await SortBy((m, x) => (x.RequiredAttackLevel + x.RequiredDefenseLevel + x.RequiredMagicLevel + x.RequiredRangedLevel + x.RequiredSlayerLevel));
        }

        private async void SortByAvailableAmount()
        {
            SetSorting("amount");
            await SortBy((m, x) => m.Amount);
        }

        private async void SortByAskingPrice()
        {
            SetSorting("ask-price");
            await SortBy((m, x) => m.PricePerItem);
        }

        private async void SortByTotal()
        {
            SetSorting("total");
            await SortBy((m, x) => LotTotal(m));
        }

        private async void SortByExpiryDate()
        {
            SetSorting("expires");
            await SortBy((m, x) => m.Expires);
        }

        private async void SortBySeller()
        {
            SetSorting("seller");
            await SortBy((m, x) => GetUserName(m.SellerUserId));
        }

        private async void SortByStats()
        {
            SetSorting("stats");
            await SortBy((m, x) => (x.ArmorPower + x.MagicPower + x.RangedPower + x.WeaponPower + x.MagicAim + x.RangedAim + x.WeaponAim));
        }

        private async Task SortBy<T>(Func<MarketItem, Models.Item, T> sort)
        {
            try
            {
                await Task.Run(() =>
                {
                    if (sortDirection == SortDirection.Descending)
                    {
                        filtered = filtered.OrderByDescending(m => sort(m, GetItem(m.ItemId))).ToList();
                        return;
                    }

                    filtered = filtered.OrderBy(m => sort(m, GetItem(m.ItemId))).ToList();
                });
            }
            finally
            {
                StateHasChanged();
            }
        }

        private void SetSorting(string value)
        {
            if (sort != value)
            {
                sortDirection = SortDirection.Ascending;
            }
            else
            {
                sortDirection = (SortDirection)(((int)sortDirection + 1) % Enum.GetValues(typeof(SortDirection)).Length);
            }
            sort = value;
        }
    }
}

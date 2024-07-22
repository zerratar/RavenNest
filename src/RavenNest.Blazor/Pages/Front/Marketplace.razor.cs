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
        private bool isAdmin;
        private bool canCancelItems;

        private bool sorting;
        private string sort = "name";
        private SortDirection sortDirection;
        private ItemFilter itemFilter = ItemFilter.All;

        protected override async Task OnInitializedAsync()
        {
            session = AuthService.GetSession();
            isAdmin = session != null && session.Administrator;
            items = await MarketplaceService.GetMarketItemsAsync();
            canCancelItems = isAdmin || items.Any(CanCancelItem);
        }

        private async Task SelectItemFilter(ItemFilter newItemFilter)
        {
            itemFilter = newItemFilter;
            items = await MarketplaceService.GetMarketItemsAsync(newItemFilter);
            InvokeAsync(StateHasChanged);
        }
        private bool CanCancelItem(Guid itemListing)
        {
            if (isAdmin) return true;
            if (session == null || items == null || items.Count == 0)
                return false;
            return CanCancelItem(items.FirstOrDefault(x => x.Id == itemListing));
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

        private async void CancelListing(Guid id)
        {
            if (!CanCancelItem(id)) return;
            if (await MarketplaceService.CancelListingAsync(id))
            {
                var itemToRemove = items.FirstOrDefault(x => x.Id == id);
                if (itemToRemove != null)
                {
                    items.Remove(itemToRemove);
                }
                else
                {
                    items = await MarketplaceService.GetMarketItemsAsync();
                }
                await InvokeAsync(StateHasChanged);
            }
        }

        private async void CancelExpiredListings()
        {
            if (!isAdmin) return;

            var itemsToCancel = new List<Guid>();
            foreach (var item in items)
            {
                if (item.Expires < DateTime.UtcNow)
                {
                    itemsToCancel.Add(item.Id);
                }
            }

            if (await MarketplaceService.CancelListingsAsync(itemsToCancel))
            {
                items = await MarketplaceService.GetMarketItemsAsync();
                await InvokeAsync(StateHasChanged);
            }
        }

        private MarkupString GetIndicator(string value)
        {
            if (value != sort)
            {
                return (MarkupString)"";
            }

            return (MarkupString)(sortDirection == SortDirection.Ascending ? "&uarr;" : "&darr;");
        }

        private async void SortByName()
        {
            SetSorting("name");
            await SortBy((m, x) => x.Name);
        }
        private async void SortByLevelReq()
        {
            SetSorting("lev-req");
            await SortBy((m, x) => (x.RequiredAttackLevel + x.RequiredDefenseLevel + x.RequiredMagicLevel + x.RequiredRangedLevel + x.RequiredSlayerLevel));
        }
        private async void SortByCategory()
        {
            SetSorting("category");
            await SortBy((m, x) => (int)x.Category);
        }
        private async void SortByType()
        {
            SetSorting("type");
            await SortBy((m, x) => (int)x.Type);
        }
        private async void SortByMaterial()
        {
            SetSorting("material");
            await SortBy((m, x) => ItemService.GetMaterialIndex(x));
        }
        private async void SortByVendorPrice()
        {
            SetSorting("vendor");
            await SortBy((m, x) => x.ShopSellPrice);
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

        private async void SortByCrafting()
        {
            //SetSorting("cra-req");
            //await SortBy(x => x.RequiredCraftingLevel);
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
                sorting = true;
                await Task.Run(() =>
                {
                    if (sortDirection == SortDirection.Descending)
                    {
                        var newMarketItems = this.items.OrderByDescending(m => sort(m, GetItem(m.ItemId)));
                        var updated = new MarketItemCollection();
                        updated.Total = items.Total;
                        updated.AddRange(newMarketItems);
                        items = updated;
                    }
                    else
                    {
                        var newMarketItems = this.items.OrderBy(m => sort(m, GetItem(m.ItemId)));
                        var updated = new MarketItemCollection();
                        updated.Total = items.Total;
                        updated.AddRange(newMarketItems);
                        items = updated;
                    }
                });
            }
            finally
            {
                sorting = false;
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

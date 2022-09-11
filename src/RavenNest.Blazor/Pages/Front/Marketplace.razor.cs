using System;
using System.Linq;
using System.Threading.Tasks;

namespace RavenNest.Blazor.Pages.Front
{
    public partial class Marketplace
    {
        private Sessions.SessionInfo session;
        private RavenNest.Models.MarketItemCollection items;
        private bool isAdmin;
        private bool canCancelItems;
        protected override async Task OnInitializedAsync()
        {
            session = AuthService.GetSession();
            isAdmin = session != null && session.Administrator;
            items = await MarketplaceService.GetMarketItemsAsync();
            canCancelItems = isAdmin || items.Any(CanCancelItem);
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

        private string GetUserName(string twitchUserId)
        {
            if (!isAdmin) return null;
            return UserService.GetUser(twitchUserId)?.UserName;
        }

        private async void CancelListing(Guid id)
        {
            if (!CanCancelItem(id)) return;
            if (await MarketplaceService.CancelListingAsync(id))
            {
                items = await MarketplaceService.GetMarketItemsAsync();
                await InvokeAsync(StateHasChanged);
            }
        }
    }
}

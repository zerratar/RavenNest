using System;
using System.Threading.Tasks;

namespace RavenNest.Blazor.Pages.Front
{
    public partial class Marketplace
    {
        private Sessions.SessionInfo session;
        private RavenNest.Models.MarketItemCollection items;
        private bool isAdmin;

        protected override async Task OnInitializedAsync()
        {
            session = AuthService.GetSession();
            isAdmin = session != null && session.Administrator;
            items = await MarketplaceService.GetMarketItemsAsync();
        }

        private RavenNest.Models.Item GetItem(Guid itemId)
        {
            return ItemService.GetItem(itemId);
        }

        private async void CancelListing(Guid id)
        {
            if (await MarketplaceService.CancelListingAsync(id))
            {
                await InvokeAsync(StateHasChanged);
            }
        }
    }
}

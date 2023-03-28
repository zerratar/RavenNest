using RavenNest.Blazor.Extensions;
using RavenNest.Blazor.Services;
using System.Net.Http;
using System.Threading.Tasks;

namespace RavenNest.Blazor.Pages.Dashboard
{
    public partial class PoQ
    {
        private Models.SessionInfo session;
        private string code;
        private PoQAuthToken token;
        private string UserDetails;

        protected override async Task OnInitializedAsync()
        {
            session = AuthService.GetSession();
            await HandleLoginCallbackAsync();
        }

        protected override void OnAfterRender(bool firstRender)
        {
            if (session == null || !session.Authenticated)
            {
                NavManager.NavigateTo("/login");
            }
        }

        private async Task HandleLoginCallbackAsync()
        {
            if (token != null)
            {
                return;
            }

            if (NavManager.TryGetQueryString<string>("code", out code))
            {
                token = await PoQService.RequestAccessTokenAsync(code);
                if (token == null || string.IsNullOrEmpty(token.access_token))
                {
                    UserDetails = "Access token is nuUuuuuuUUULL!";
                    return;
                }

                UserDetails = await PoQService.DoPoqRequestAsync("api/v1/users/me");
            }
        }

        private void Login()
        {
            var authUrl = PoQService.AuthorizeUrl;
            NavManager.NavigateTo(authUrl);
        }
    }

}

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.JSInterop;
using System;

namespace RavenNest.Blazor.Shared
{
    public partial class CookieDisclaimer
    {
        private bool visible;
        private string cookieString;
        private ITrackingConsentFeature consentFeature;

        protected override void OnInitialized()
        {
            consentFeature = CookieService.Context.Features.Get<ITrackingConsentFeature>();
            visible = !consentFeature?.CanTrack ?? false;
            cookieString = consentFeature?.CreateConsentCookie();
        }

        private void ReadMore()
        {
            NavigationManager.NavigateTo("/cookies");
        }

        private void CloseAndAcceptCookies()
        {
            visible = false;
            CookieService.AcceptDisclaimer();
            JSRuntime.InvokeVoidAsync(
                "cookies.acceptMessage",
                cookieString);

            InvokeAsync(StateHasChanged);
        }
    }
}

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
            if (CookieService == null || CookieService.Context == null)
            {
                visible = false;
                return;
            }

            try
            {
                consentFeature = CookieService.Context.Features.Get<ITrackingConsentFeature>();
                visible = !consentFeature?.CanTrack ?? false;
                cookieString = consentFeature?.CreateConsentCookie();
            }
            catch (Exception exc)
            {
                visible = false;
            }
        }

        private void ReadMore()
        {
            if (!visible)
                return;

            NavigationManager.NavigateTo("/cookies");
        }

        private void CloseAndAcceptCookies()
        {
            if (!visible)
                return;

            visible = false;
            CookieService.AcceptDisclaimer();
            JSRuntime.InvokeVoidAsync(
                "cookies.acceptMessage",
                cookieString);

            InvokeAsync(StateHasChanged);
        }
    }
}

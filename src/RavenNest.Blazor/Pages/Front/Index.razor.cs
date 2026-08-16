using Microsoft.JSInterop;
using RavenNest.Blazor.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RavenNest.Blazor.Pages.Front
{
    public partial class Index
    {
        private IReadOnlyList<TwitchStream> twitchStreams;

        /// <summary>The latest few, so the landing page shows what changed without being a blog.</summary>
        private IReadOnlyList<RavenNest.Blazor.Services.Announcements.Announcement> announcements =
            Array.Empty<RavenNest.Blazor.Services.Announcements.Announcement>();

        /// <summary>
        ///     The soonest change that has not happened yet, if there is one. Only ever one: a row
        ///     of warnings is a wall, and the next thing to happen is the one that matters.
        /// </summary>
        private RavenNest.Blazor.Services.Announcements.Announcement upcoming;

        /// <summary>
        ///     Whether this visitor has put the banner away, and whether we have looked yet.
        /// </summary>
        /// <remarks>
        ///     Nothing renders until the answer is known. Rendering first and hiding afterwards
        ///     would show the banner for a frame to everybody who had already dismissed it, which
        ///     is worse than it appearing a moment late.
        /// </remarks>
        private bool upcomingDismissed;
        private bool dismissalChecked;

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (!firstRender || upcoming == null || dismissalChecked)
            {
                // Blazor Server cannot reach the browser before the first render, so this is the
                // earliest the answer can be had.
                if (firstRender && upcoming == null) dismissalChecked = true;
                return;
            }

            try
            {
                upcomingDismissed = await JS.InvokeAsync<bool>("rfDismiss.isDismissed", upcoming.Id.ToString());
            }
            catch
            {
                // Storage refused or the helper is missing. Showing it is the harmless direction.
                upcomingDismissed = false;
            }

            dismissalChecked = true;
            StateHasChanged();
        }

        private async Task DismissUpcoming()
        {
            upcomingDismissed = true;

            try
            {
                await JS.InvokeVoidAsync("rfDismiss.dismiss", upcoming.Id.ToString());
            }
            catch
            {
                // It comes back next visit, which is the right way for this to fail.
            }
        }

        protected override async Task OnInitializedAsync()
        {
            announcements = Announcements.GetPublished(3);

            upcoming = Announcements.GetPublished()
                .Where(x => x.IsUpcoming)
                .OrderBy(x => x.EffectiveUtc)
                .FirstOrDefault();
            twitchStreams = await GetTwitchStreamsAsync(6);
        }

        public async Task<IReadOnlyList<TwitchStream>> GetTwitchStreamsAsync(int take)
        {
            var random = new Random();
            return await Task.Run(() => TwitchService
                    .GetTwitchStreams()
                    .OrderBy(x => random.Next())
                    .Take(take)
                    .ToList());
        }

        private void ShowStream(TwitchStream stream)
        {
            if (stream.IsVisible) return;
            stream.IsVisible = true;
            InvokeAsync(StateHasChanged);
        }
        public void OpenNewsPage()
        {
            NavigationManager.NavigateTo("https://medium.com/ravenfall");
        }
    }
}

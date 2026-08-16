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

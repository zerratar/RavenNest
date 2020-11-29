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

        protected override async Task OnInitializedAsync()
        {
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
    }
}

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TwitchLib.Api;
using TwitchLib.Api.Helix.Models.Subscriptions;

namespace RavenNest.BusinessLogic.Game
{
    public class TwitchClient : ITwitchClient
    {
        private readonly AppSettings appSettings;
        private readonly TwitchAPI api;
        private readonly ILogger logger;

        public TwitchClient(
            ILogger<TwitchClient> logger,
            Microsoft.Extensions.Options.IOptions<AppSettings> appSettings)
        {
            this.appSettings = appSettings.Value;
            api = new TwitchAPI();
            api.Settings.ClientId = "gp762nuuoqcoxypju8c569th9wz7q5";//this.appSettings.TwitchClientId;
            //api.Settings.Secret = this.appSettings.TwitchClientSecret;
            api.Settings.AccessToken = this.appSettings.TwitchAccessToken;
            this.logger = logger;
        }

        public async Task<RavenNest.Twitch.TwitchRequests.TwitchUser> GetUserByIdAsync(string twitchUserId)
        {
            if (string.IsNullOrEmpty(twitchUserId))
            {
                return null;
            }

            var users = await GetUsersByIdAsync(new[] { twitchUserId });
            return users.Count > 0 ? users[0] : null;
        }

        public async Task<IReadOnlyList<RavenNest.Twitch.TwitchRequests.TwitchUser>> GetUsersByIdAsync(
            IReadOnlyList<string> twitchUserIds)
        {
            if (twitchUserIds == null || twitchUserIds.Count == 0)
            {
                return Array.Empty<RavenNest.Twitch.TwitchRequests.TwitchUser>();
            }

            try
            {
                // Built per call rather than held. It caches an app token internally, and the two
                // callers are session start and the periodic rename check, neither of which is hot.
                var requests = new RavenNest.Twitch.TwitchRequests(
                    null, appSettings.TwitchClientId, appSettings.TwitchClientSecret);

                return await requests.GetUsersByIdAsync(twitchUserIds);
            }
            catch (Exception exc)
            {
                // A rename check is a nice to have. Twitch being unreachable must never stop someone
                // from starting their stream, so this is logged and swallowed.
                logger.LogError("Unable to resolve Twitch users: " + exc);
                return Array.Empty<RavenNest.Twitch.TwitchRequests.TwitchUser>();
            }
        }

        public async Task<Subscription> GetSubscriberAsync(string userId)
        {
            try
            {
                var subInfo = await api.Helix.Subscriptions.GetUserSubscriptionsAsync(appSettings.OriginBroadcasterId, new List<string> { userId });
                if (subInfo == null || subInfo.Data?.Length == 0) return null;
                return subInfo.Data[0];
            }
            catch (Exception exc)
            {
                logger.LogError(exc.ToString());
                return null;
            }
        }
    }
}
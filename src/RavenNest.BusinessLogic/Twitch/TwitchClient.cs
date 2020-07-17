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
            api.Settings.ClientId = this.appSettings.TwitchClientId;
            api.Settings.Secret = this.appSettings.TwitchClientSecret;
            api.Settings.AccessToken = this.appSettings.TwitchAccessToken;
            this.logger = logger;
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
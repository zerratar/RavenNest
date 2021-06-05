using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using RavenNest.BusinessLogic;
using RavenNest.BusinessLogic.Data;
using RavenNest.Sessions;
using RavenNest.Twitch;
using System;
using System.Net;
using System.Threading.Tasks;

namespace RavenNest.Blazor.Services
{
    public class LogoService : RavenNestService
    {
        private readonly IMemoryCache memoryCache;
        private readonly IGameData gameData;
        private readonly AppSettings settings;
        public LogoService(
            IOptions<AppSettings> settings,
            IGameData gameData,
            IMemoryCache memoryCache,
            IHttpContextAccessor accessor,
            ISessionInfoProvider sessionInfoProvider)
            : base(accessor, sessionInfoProvider)
        {
            this.settings = settings.Value;
            this.memoryCache = memoryCache;
            this.gameData = gameData;
        }

        public async Task UpdateClanLogoAsync(string userId)
        {
            await GetClanLogoAsync("_" + userId);
            await GetChannelPictureAsync("_" + userId);
        }

        public bool ClearLogos(string userId)
        {
            var success = false;
            try
            {
                memoryCache.Remove("logo_" + userId);
                success = true;
            }
            catch { }

            try
            {
                memoryCache.Remove("clan_logo_" + userId);
                success = true;
            }
            catch { }

            return success;
        }

        public async Task<byte[]> GetChannelPictureAsync(string userId)
        {
            try
            {
                var forceRefreshLogo = userId.Contains("_");
                if (forceRefreshLogo)
                {
                    userId = userId.Split('_')[1];
                }
                else
                {
                    if (memoryCache != null && memoryCache.TryGetValue("logo_" + userId, out var logoData) && logoData is byte[] data)
                    {
                        return data;
                    }
                }

                var twitch = new TwitchRequests(clientId: settings.TwitchClientId, clientSecret: settings.TwitchClientSecret);
                var profile = await twitch.GetUserAsync(userId);
                if (profile != null)
                {
                    using (var wc = new WebClient())
                    {
                        var binaryData = await wc.DownloadDataTaskAsync(new Uri(profile.logo));
                        return memoryCache.Set("logo_" + userId, binaryData);
                    }
                }

            }
            catch { }

            return null;
        }

        public async Task<byte[]> GetClanLogoAsync(string userId)
        {
            try
            {
                string logoUrl = null;
                var forceRefreshLogo = userId.Contains("_");
                if (forceRefreshLogo)
                {
                    userId = userId.Split('_')[1];

                    var user = gameData.GetUser(userId);
                    if (user != null)
                    {
                        var clan = gameData.GetClanByUser(user.Id);
                        if (clan != null && clan.Logo != null && clan.Logo.Contains("/api/twitch/logo/"))
                        {
                            clan.Logo = null;
                        }

                        logoUrl = clan?.Logo;
                    }
                }
                else
                {
                    if (memoryCache != null && memoryCache.TryGetValue("clan_logo_" + userId, out var logoData) && logoData is byte[] data)
                    {
                        return data;
                    }
                }

                if (string.IsNullOrEmpty(logoUrl))
                {
                    var twitch = new TwitchRequests(clientId: settings.TwitchClientId, clientSecret: settings.TwitchClientSecret);
                    var profile = await twitch.GetUserAsync(userId);
                    if (profile != null)
                    {
                        logoUrl = profile.logo;
                    }
                }

                if (!string.IsNullOrEmpty(logoUrl))
                {
                    using (var wc = new WebClient())
                    {
                        var binaryData = await wc.DownloadDataTaskAsync(new Uri(logoUrl));
                        return memoryCache.Set("clan_logo_" + userId, binaryData);
                    }
                }
            }
            catch { }
            return null;
        }

    }
}

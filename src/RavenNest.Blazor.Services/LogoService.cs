using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using RavenNest.BusinessLogic;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Providers;
using RavenNest.Sessions;
using RavenNest.Twitch;
using System;
using System.Net;
using System.Threading.Tasks;

namespace RavenNest.Blazor.Services
{
    public class LogoService : RavenNestService
    {
        private readonly IMemoryFileCache fileCache;
        private readonly GameData gameData;
        private readonly AppSettings settings;
        public LogoService(
            IOptions<AppSettings> settings,
            GameData gameData,
            IMemoryFileCacheProvider fileCacheProvider,
            IHttpContextAccessor accessor,
            SessionInfoProvider sessionInfoProvider)
            : base(accessor, sessionInfoProvider)
        {
            this.settings = settings.Value;
            this.fileCache = fileCacheProvider.Get("user_images", ".png");
            this.gameData = gameData;
        }

        public async Task UpdateClanLogoAsync(Guid userId)
        {
            var twitch = gameData.GetUserAccess(userId, "twitch");
            if (twitch != null)
            {
                await GetClanLogoAsync("_" + twitch.PlatformId);
                await GetChannelPictureAsync("_" + twitch.PlatformId);
            }
        }

        public async Task UpdateClanLogoAsync(string userId)
        {
            await GetClanLogoAsync("_" + userId);
            await GetChannelPictureAsync("_" + userId);
        }

        public async Task UpdateUserLogosAsync(TwitchRequests.TwitchUser twitchUser)
        {
            await DownloadLogoAsync(twitchUser.Id, twitchUser.ProfileImageUrl);
            await DownloadClanLogoAsync(twitchUser.Id, twitchUser.ProfileImageUrl);
        }

        public bool ClearLogos(string userId)
        {
            var success = false;
            try
            {
                fileCache.Remove("logo_" + userId);
                success = true;
            }
            catch { }

            try
            {
                fileCache.Remove("clan_logo_" + userId);
                success = true;
            }
            catch { }

            return success;
        }

        public async Task<byte[]> GetChannelPictureAsync(string twitchUserId, string downloadUrl = null)
        {
            try
            {
                var forceRefreshLogo = twitchUserId.Contains('_');
                if (forceRefreshLogo)
                {
                    twitchUserId = twitchUserId.Split('_')[1];
                }
                else
                {
                    if (fileCache != null && fileCache.TryGetValue("logo_" + twitchUserId, out var logoData) && logoData is byte[] data)
                    {
                        return data;
                    }
                }

                //var twitch = new TwitchRequests(clientId: settings.TwitchClientId, clientSecret: settings.TwitchClientSecret);
                //var profile = await twitch.KGetUserAsync(userId);

                return await DownloadLogoAsync(twitchUserId, downloadUrl);

            }
            catch { }

            return null;
        }

        public async Task<byte[]> GetClanLogoAsync(string userId, string downloadUrl = null)
        {
            try
            {
                string logoUrl = downloadUrl;
                var forceRefreshLogo = userId.Contains('_');
                if (forceRefreshLogo)
                {
                    userId = userId.Split('_')[1];

                    var user = gameData.GetUserByTwitchId(userId);
                    if (user != null)
                    {
                        var clan = gameData.GetClanByOwner(user.Id);
                        if (clan != null && clan.Logo != null && clan.Logo.Contains("/api/twitch/logo/"))
                        {
                            clan.Logo = null;
                        }

                        logoUrl = clan?.Logo;
                    }
                }
                else
                {
                    if (fileCache != null && fileCache.TryGetValue("clan_logo_" + userId, out var logoData) && logoData is byte[] data)
                    {
                        return data;
                    }

                    // Not cached: the clan logo was only ever stored when the owner signed in to the website with
                    // Twitch, so every other owner's capes showed the placeholder in the game while the website (which
                    // shows the owner's channel picture) looked right. The clan's own logo when it has one, otherwise
                    // that same channel picture.
                    var owner = gameData.GetUserByTwitchId(userId);
                    var ownerClan = owner != null ? gameData.GetClanByOwner(owner.Id) : null;
                    if (ownerClan != null && !string.IsNullOrWhiteSpace(ownerClan.Logo)
                        && ownerClan.Logo.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                        && !ownerClan.Logo.Contains("/api/twitch/logo/") && !ownerClan.Logo.Contains("/api/clan/logo/"))
                    {
                        var custom = await DownloadClanLogoAsync(userId, ownerClan.Logo);
                        if (custom != null) return custom;
                    }

                    return await GetChannelPictureAsync(userId);
                }

                //if (string.IsNullOrEmpty(logoUrl))
                //{
                //    var twitch = new TwitchRequests(clientId: settings.TwitchClientId, clientSecret: settings.TwitchClientSecret);
                //    var profile = await twitch.GetUserAsync(userId);
                //    if (profile != null)
                //    {
                //        logoUrl = profile.logo;
                //    }
                //}

                return await DownloadClanLogoAsync(userId, logoUrl);
            }
            catch { }
            return null;
        }

        private async Task<byte[]> DownloadLogoAsync(string userId, string downloadUrl)
        {
            return await DownloadAndStoreInCacheAsync("logo_" + userId, downloadUrl);
        }

        private async Task<byte[]> DownloadClanLogoAsync(string userId, string downloadUrl)
        {
            return await DownloadAndStoreInCacheAsync("clan_logo_" + userId, downloadUrl);
        }

        private async Task<byte[]> DownloadAndStoreInCacheAsync(string key, string downloadUrl)
        {
            try
            {
                if (!string.IsNullOrEmpty(downloadUrl))
                {
                    using (var wc = new WebClient())
                    {
                        var binaryData = await wc.DownloadDataTaskAsync(new Uri(downloadUrl));
                        return fileCache.Set(key, binaryData);
                    }
                }
            }
            catch
            {
            }

            return null;
        }
    }
}

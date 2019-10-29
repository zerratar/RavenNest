using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using RavenNest.BusinessLogic;
using RavenNest.BusinessLogic.Docs.Attributes;
using RavenNest.BusinessLogic.Game;
using RavenNest.Sessions;
using RavenNest.Twitch;

namespace RavenNest.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [ApiDescriptor(Name = "Twitch API", Description = "Used by the website to allow authentication with Twitch. This is not meant to be used elsewhere.")]
    public class TwitchController : ControllerBase
    {
        private readonly IPlayerManager playerManager;
        private readonly ISessionInfoProvider sessionInfoProvider;
        private readonly IMemoryCache memoryCache;
        private readonly AppSettings settings;

        public TwitchController(
            IOptions<AppSettings> settings,
            IPlayerManager playerManager,
            ISessionInfoProvider sessionInfoProvider,
            IMemoryCache memoryCache)
        {
            this.playerManager = playerManager;
            this.sessionInfoProvider = sessionInfoProvider;
            this.memoryCache = memoryCache;
            this.settings = settings.Value;
        }

        [HttpGet("logo/{userId}")]
        public async Task<ActionResult> GetChannelPictureAsync(string userId)
        {
            try
            {
                if (memoryCache != null && memoryCache.TryGetValue("logo_" + userId, out var logoData) && logoData is byte[] data)
                {
                    return File(data, "image/png");
                }

                var twitch = new TwitchRequests(clientId: settings.TwitchClientId, clientSecret: settings.TwitchClientSecret);
                var profile = await twitch.GetUserAsync(userId);
                if (profile != null)
                {
                    using (var wc = new WebClient())
                    {
                        var binaryData = await wc.DownloadDataTaskAsync(new Uri(profile.logo));
                        return File(memoryCache.Set("logo_" + userId, binaryData), "image/png");
                    }
                }
                return NotFound();
            }
            catch { return NotFound(); }
        }

        [HttpGet("session")]
        [MethodDescriptor(Name = "Set Twitch Access Token", Description = "Updates current session with the set Twitch access token, used as an user identifier throughout the website.")]
        public async Task<SessionInfo> SetAccessToken(string token)
        {
            var session = this.HttpContext.Session;
            var result = await sessionInfoProvider.SetTwitchTokenAsync(session, token);
            var user = await sessionInfoProvider.GetTwitchUserAsync(session, token);
            if (user != null)
            {
                playerManager.CreatePlayerIfNotExists(user.Id, user.Login);
            }
            return result;
        }

        [HttpGet("access")]
        [MethodDescriptor(Name = "Get Access Token Request URL", Description = "Gets a Twitch access token request url with the scope user:read:email.")]
        public string GetAccessTokenRequestUrl()
        {
#if DEBUG
            return $"https://id.twitch.tv/oauth2/authorize?client_id={settings.TwitchClientId}&redirect_uri="
            + "https://localhost:5001/login"
            + "&response_type=token&scope=user:read:email";
#else
            return $"https://id.twitch.tv/oauth2/authorize?client_id={settings.TwitchClientId}&redirect_uri="
            + "https://www.ravenfall.stream/login"
            + "&response_type=token&scope=user:read:email";
#endif
        }

        [HttpGet("user")]
        [MethodDescriptor(Name = "Get Twitch User", Description = "After authenticating with Twitch, this can be used to get information about the logged in user.")]
        public async Task<string> GetTwitchUser()
        {
            if (!this.sessionInfoProvider.TryGetTwitchToken(HttpContext.Session, out var key))
            {
                return "nope";
            }

            var twitch = new TwitchRequests(key);
            var twitchUser = await twitch.GetUsersAsync();
            await this.sessionInfoProvider.SetTwitchUserAsync(HttpContext.Session, twitchUser);
            return twitchUser;
        }
    }
}
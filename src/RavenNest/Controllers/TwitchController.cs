using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using RavenNest.BusinessLogic;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Docs.Attributes;
using RavenNest.BusinessLogic.Game;
using RavenNest.Models;
using RavenNest.Sessions;
using RavenNest.Twitch;
using static RavenNest.Twitch.TwitchRequests;

namespace RavenNest.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [ApiDescriptor(Name = "Twitch API", Description = "Used by the website to allow authentication with Twitch. This is not meant to be used elsewhere.")]
    public class TwitchController : ControllerBase
    {
        private readonly IPlayerManager playerManager;
        private readonly IGameData gameData;
        private readonly ISessionInfoProvider sessionInfoProvider;
        private readonly IMemoryCache memoryCache;
        private readonly IAuthManager authManager;
        private readonly AppSettings settings;

        public TwitchController(
            IOptions<AppSettings> settings,
            IPlayerManager playerManager,
            IGameData gameData,
            ISessionInfoProvider sessionInfoProvider,
            IMemoryCache memoryCache,
            IAuthManager authManager)
        {
            this.playerManager = playerManager;
            this.gameData = gameData;
            this.sessionInfoProvider = sessionInfoProvider;
            this.memoryCache = memoryCache;
            this.authManager = authManager;
            this.settings = settings.Value;
        }

        [HttpGet("authorize")]
        public async Task<ActionResult> OAuthAuthorize()
        {
            var reqCode = HttpContext.Request.Query["code"];
            var reqState = HttpContext.Request.Query["state"];
            var requestUrl = "https://www.ravenfall.stream/login?code=" + reqCode + "&state=" + reqState;
            try
            {
                var sessionInfo = await GetTwitchUserAsync(reqCode);
                if (sessionInfo != null)
                {
                    requestUrl += "&id=" + sessionInfo.UserID + "&user=" + sessionInfo.Login;
                }
            }
            catch
            {
                // ignored
            }

            return Redirect(requestUrl);
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
            if (sessionInfoProvider.TryGet(HttpContext.Session, out var session))
            {
                return $"{{ \"login\": \"{session.UserName}\", \"id\": \"{session.UserId}\"}}";
            }

            if (!this.sessionInfoProvider.TryGetTwitchToken(HttpContext.Session, out var key))
            {
                return "nope";
            }

            var twitch = new TwitchRequests(key, settings.TwitchClientId, settings.TwitchClientSecret);
            var twitchUser = await twitch.GetUserAsync();
            await this.sessionInfoProvider.SetTwitchUserAsync(HttpContext.Session, twitchUser);
            return twitchUser;
        }

        private async Task<TwitchValidateResponse> GetTwitchUserAsync(string key)
        {
            var twitch = new TwitchRequests(key, settings.TwitchClientId, settings.TwitchClientSecret);
            return await twitch.ValidateOAuthTokenAsync();
        }


        private AuthToken GetAuthToken()
        {
            if (HttpContext.Request.Headers.TryGetValue("auth-token", out var value))
            {
                return authManager.Get(value);
            }
            return null;
        }
    }
}
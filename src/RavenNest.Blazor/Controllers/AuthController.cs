using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using RavenNest.Blazor.Services;
using RavenNest.BusinessLogic.Game;
using RavenNest.Models;
using RavenNest.Sessions;

namespace RavenNest.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthManager authManager;
        private readonly SessionInfoProvider sessionInfoProvider;
        private readonly LogoService logoService;

        public AuthController(
            IAuthManager authManager,
            SessionInfoProvider sessionInfoProvider,
            LogoService logoService)
        {
            this.authManager = authManager;
            this.sessionInfoProvider = sessionInfoProvider;
            this.logoService = logoService;
        }

        [HttpGet("activate-pubsub")]
        public ActionResult PubsubRedirect()
        {
            const string TwitchClientID = "757vrtjoawg2rtquprnfb35nqah1w4";
            const string TwitchRedirectUri = "https://id.twitch.tv/oauth2/authorize";

            List<StateParameters> stateParameters = new();
            stateParameters.Add(new("pubsub", "true"));

            var encodedState = authManager.GetRandomizedBase64EncodedStateParameters(stateParameters);

            return Redirect(TwitchRedirectUri + "?response_type=token" +
                $"&client_id={TwitchClientID}" +
//#if DEBUG
//                $"&redirect_uri=https://localhost:5001/login/twitch" +
//#else
                $"&redirect_uri=https://www.ravenfall.stream/login/twitch" +
//#endif
                $"&scope=user:read:email+bits:read+chat:read+chat:edit+channel:read:subscriptions+channel:read:redemptions+channel:read:predictions" +
                $"&state={encodedState} " +
                $"&force_verify=true");

        }

        [HttpGet]
        public string Get()
        {
            var token = GetAuthToken();
            if (token == null)
            {
                return "Not logged in";
            }

            return "You are logged in";
        }

        [HttpPost]
        public AuthToken AuthenticateAsync(AuthModel model)
        {
            return this.authManager.Authenticate(model.Username, model.Password);
        }

        [HttpPost("login")]
        public async Task<SessionInfo> LoginAsync(AuthModel model)
        {
            var authenticateAsync = this.authManager.Authenticate(model.Username, model.Password);
            return (await sessionInfoProvider.SetAuthTokenAsync(SessionId, authenticateAsync)).SessionInfo;
        }

        [HttpGet("logout")]
        public SessionInfo Logout()
        {
            sessionInfoProvider.Clear(SessionId);
            return new SessionInfo();
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("signup")]
        public async Task<SessionInfo> SignUpAsync(PasswordModel password)
        {
            var user = await sessionInfoProvider.GetTwitchUserAsync(SessionId);
            authManager.SignUp(user.Id, user.Login, user.DisplayName, user.Email, password.Password);
            var result = await sessionInfoProvider.StoreAsync(SessionId);
            await logoService.UpdateUserLogosAsync(result.TwitchUser);
            return result.SessionInfo;
        }

        private string SessionId => HttpContext.GetSessionId();

        private AuthToken GetAuthToken()
        {
            if (HttpContext.Request.Headers.TryGetValue("auth-token", out var value))
            {
                return authManager.Get(value);
            }
            return null;
        }
        public class AuthModel
        {
            public string Username { get; set; }
            public string Password { get; set; }
        }

        public class PasswordModel
        {
            public string Password { get; set; }
        }
    }
}

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using RavenNest.Blazor.Services.Models;
using RavenNest.BusinessLogic;
using RavenNest.BusinessLogic.Game;
using RavenNest.Sessions;
using System;
using System.Threading.Tasks;

namespace RavenNest.Blazor.Services
{
    public class AuthService
    {
        private readonly IAuthManager authManager;
        private readonly IPlayerManager playerManager;
        private readonly IHttpContextAccessor accessor;
        private readonly ISessionInfoProvider sessionInfoProvider;
        private readonly AppSettings settings;

        public AuthService(
            IOptions<AppSettings> settings,
            IAuthManager authManager,
            IPlayerManager playerManager,
            IHttpContextAccessor accessor,
            ISessionInfoProvider sessionInfoProvider)
        {
            this.authManager = authManager;
            this.playerManager = playerManager;
            this.accessor = accessor;
            this.sessionInfoProvider = sessionInfoProvider;
            this.settings = settings.Value;
        }

        public HttpContext Context => accessor.HttpContext;
        public ISession Session => accessor.HttpContext.Session;

        public SessionInfo GetSession()
        {
            var id = SessionCookie.GetSessionId(Context);
            if (!this.sessionInfoProvider.TryGet(id, out var sessionInfo))
                sessionInfo = new SessionInfo();
            return sessionInfo;
        }

        public void Logout()
        {
            var session = Context.GetSessionId();
            sessionInfoProvider.Clear(session);
        }

        public async Task<SessionInfo> TwitchLoginAsync(string accessToken)
        {
            var session = Context.GetSessionId();
            var result = await sessionInfoProvider.SetTwitchTokenAsync(session, accessToken);
            var user = await sessionInfoProvider.GetTwitchUserAsync(session, accessToken);
            if (user != null)
            {
                playerManager.CreatePlayerIfNotExists(user.Id, user.Login, "1");
            }
            return result;
        }

        public Task<SessionInfo> LoginAsync(UserLoginModel model)
        {
            var id = SessionCookie.GetSessionId(Context);
            var auth = authManager.Authenticate(model.Username, model.Password);
            return sessionInfoProvider.SetAuthTokenAsync(id, auth);
        }

        public string GetTwitchLoginUrl()
        {
#if DEBUG
            return $"https://id.twitch.tv/oauth2/authorize?client_id={settings.TwitchClientId}&redirect_uri="
            + $"https://{Context.Request.Host}/login"
            + "&response_type=token&scope=user:read:email";
#else
            return $"https://id.twitch.tv/oauth2/authorize?client_id={settings.TwitchClientId}&redirect_uri="
            + "https://www.ravenfall.stream/login"
            + "&response_type=token&scope=user:read:email";
#endif
        }
    }
}

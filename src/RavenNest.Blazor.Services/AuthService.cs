using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using RavenNest.Blazor.Services.Models;
using RavenNest.BusinessLogic;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Game;
using RavenNest.Sessions;
using System.Threading.Tasks;

namespace RavenNest.Blazor.Services
{
    public class AuthService : RavenNestService
    {
        private readonly IGameData gameData;
        private readonly IAuthManager authManager;
        private readonly IPlayerManager playerManager;
        private readonly AppSettings settings;

        public AuthService(
            IOptions<AppSettings> settings,
            IGameData gameData,
            IAuthManager authManager,
            IPlayerManager playerManager,
            IHttpContextAccessor accessor,
            ISessionInfoProvider sessionInfoProvider)
            : base(accessor, sessionInfoProvider)
        {
            this.gameData = gameData;
            this.authManager = authManager;
            this.playerManager = playerManager;
            this.settings = settings.Value;
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

                var u = gameData.GetUser(user.Id);
                if (u != null && !u.UserName.Equals(user.Login, System.StringComparison.OrdinalIgnoreCase))
                {
                    u.UserName = user.Login;
                    result.UserName = user.Login;
                    result.UserNameChanged = true;
                }
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
            if (Context == null || Context.Request == null || Context.Request.Host == null)
            {
                return $"https://id.twitch.tv/oauth2/authorize?client_id={settings.TwitchClientId}&redirect_uri="
                    + "https://www.ravenfall.stream/login/twitch"
                    + "&response_type=token&scope=user:read:email";
            }

            return $"https://id.twitch.tv/oauth2/authorize?client_id={settings.TwitchClientId}&redirect_uri="
                   + $"https://{Context.Request.Host}/login/twitch"
                   + "&response_type=token&scope=user:read:email";
        }
    }
}

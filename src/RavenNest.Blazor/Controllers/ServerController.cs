using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Game;
using RavenNest.Models;
using RavenNest.Sessions;

namespace RavenNest.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ServerController : ControllerBase
    {
        private const string InsufficientPermissions = "You do not have permissions to call this API";
        private readonly ILogger<ServerController> logger;
        private readonly IAuthManager authManager;
        private readonly IGameData gameData;
        private readonly ISessionInfoProvider sessionInfoProvider;
        private readonly IServerManager serverManager;

        public ServerController(
            ILogger<ServerController> logger,
            IAuthManager authManager,
            IGameData gameData,
            ISessionInfoProvider sessionInfoProvider,
            IServerManager serverManager)
        {
            this.logger = logger;
            this.authManager = authManager;
            this.gameData = gameData;
            this.sessionInfoProvider = sessionInfoProvider;
            this.serverManager = serverManager;
        }

        [HttpPost("message/{time}")]
        public async Task BroadcastMessageAsync([FromBody] string message, int time)
        {
            await AssertAdminAccessAsync();
            serverManager.BroadcastMessageAsync(message, time);
        }

        [HttpGet("message/{message}/{time}")]
        public async Task BroadcastMessage1Async(string message, int time)
        {
            await AssertAdminAccessAsync();
            serverManager.BroadcastMessageAsync(message, time);
        }

        private async Task AssertAdminAccessAsync()
        {
            var authToken = GetAuthToken();
            if (authToken != null)
            {
                AssertAdminAuthToken(authToken);
                return;
            }

            var twitchUser = await sessionInfoProvider.GetTwitchUserAsync(HttpContext.GetSessionId());
            AssertAdminTwitchUser(twitchUser);
        }
        private AuthToken GetAuthToken()
        {
            if (HttpContext.Request.Headers.TryGetValue("auth-token", out var value))
                return authManager.Get(value);
            if (sessionInfoProvider.TryGetAuthToken(HttpContext.GetSessionId(), out var authToken))
                return authToken;
            return null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AssertAdminAuthToken(AuthToken authToken)
        {
            if (authToken == null) throw new Exception(InsufficientPermissions);
            var user = gameData.GetUser(authToken.UserId);
            if (!user.IsAdmin.GetValueOrDefault()) throw new Exception(InsufficientPermissions);

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AssertAdminTwitchUser(Twitch.TwitchRequests.TwitchUser twitchUser)
        {
            if (twitchUser == null) throw new Exception(InsufficientPermissions);
            var user = gameData.GetUser(twitchUser.Id);
            if (!user.IsAdmin.GetValueOrDefault()) throw new Exception(InsufficientPermissions);
        }
    }
}

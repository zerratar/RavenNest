using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Game;
using RavenNest.BusinessLogic.Net;
using RavenNest.Models;
using RavenNest.Sessions;
using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace RavenNest.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AdminController : ControllerBase
    {
        private const string InsufficientPermissions = "You do not have permissions to call this API";
        private readonly ILogger<AdminController> logger;
        private readonly IWebSocketConnectionProvider socketProvider;
        private readonly IGameData gameData;
        private readonly ISessionInfoProvider sessionInfoProvider;
        private readonly IAdminManager adminManager;
        private readonly IAuthManager authManager;

        public AdminController(
            ILogger<AdminController> logger,
            IWebSocketConnectionProvider socketProvider,
            IGameData gameData,
            ISessionInfoProvider sessionInfoProvider,
            IAdminManager adminManager,
            IAuthManager authManager)
        {
            this.logger = logger;
            this.socketProvider = socketProvider;
            this.gameData = gameData;
            this.sessionInfoProvider = sessionInfoProvider;
            this.adminManager = adminManager;
            this.authManager = authManager;
        }

        [HttpGet("refresh-permissions")]
        public async Task<bool> RefreshPermissions()
        {
            await AssertAdminAccessAsync();
            return await adminManager.RefreshPermissionsAsync();
        }

        [HttpGet("fix-index/{userId}")]
        public async Task<bool> FixIndices(string userId)
        {
            await AssertAdminAccessAsync();
            return adminManager.FixCharacterIndices(userId);
        }


        [HttpGet("crafting-req/{itemQuery}/{requirementQuery}")]
        public async Task<bool> SetCraftingRequirement(string itemQuery, string requirementQuery)
        {
            await AssertAdminAccessAsync();
            return adminManager.SetCraftingRequirements(itemQuery, requirementQuery);
        }

        //[HttpGet("fix-exp/{characterId}")]
        //public async Task<bool> FixExp(Guid characterId)
        //{
        //    await AssertAdminAccessAsync();
        //    return adminManager.FixCharacterExpGain(characterId);
        //}

        [HttpGet("fix-index")]
        public async Task<bool> FixIndex()
        {
            await AssertAdminAccessAsync();
            return adminManager.FixCharacterIndices(null);
        }

        [HttpGet("refresh-villages")]
        public async Task<bool> RefreshVillageInfo()
        {
            await AssertAdminAccessAsync();
            return adminManager.RefreshVillageInfo();
        }

        [HttpPost("item-recovery/{identifier}")]
        public async Task<bool> ItemRecovery(string identifier, [FromBody] string query)
        {
            try
            {
                await AssertAdminAccessAsync();
                return adminManager.ProcessItemRecovery(query, identifier);
            }
            catch (Exception exc)
            {
                logger.LogError(exc.ToString());
                throw;
            }
        }

        [HttpGet("item-recovery/{identifier}/{query}")]
        public async Task<bool> ItemRecoveryAsync(string identifier, string query)
        {
            try
            {
                await AssertAdminAccessAsync();
                return adminManager.ProcessItemRecovery(query, identifier);
            }
            catch (Exception exc)
            {
                logger.LogError(exc.ToString());
                throw;
            }
        }

        [HttpGet("kill-sockets")]
        public async Task KillConnections()
        {
            await AssertAdminAccessAsync();
            socketProvider.KillAllConnections();
        }

        [HttpGet("nerf-items")]
        public async Task<bool> NerfItemStacks()
        {
            await AssertAdminAccessAsync();
            return adminManager.NerfItems();
        }


        [HttpGet("players/{offset}/{size}/{order}/{query}")]
        public async Task<PagedPlayerCollection> GetPlayers(int offset, int size, string order, string query)
        {
            await AssertAdminAccessAsync();
            return adminManager.GetPlayersPaged(offset, size, order, query);
        }

        [HttpGet("sessions/{offset}/{size}/{order}/{query}")]
        public async Task<PagedSessionCollection> GetSessions(int offset, int size, string order, string query)
        {
            await AssertAdminAccessAsync();
            return adminManager.GetSessionsPaged(offset, size, order, query);
        }

        [HttpGet("mergeplayer/{userid}")]
        public async Task<bool> MergePlayerAccounts(string userid)
        {
            await AssertAdminAccessAsync();
            return adminManager.MergePlayerAccounts(userid);
        }

        [HttpGet("resetpassword/{userid}")]
        public async Task<bool> ResetUserPassword(string userid)
        {
            await AssertAdminAccessAsync();
            return adminManager.ResetUserPassword(userid);
        }

        [HttpGet("updateplayername/{characterId}/{name}")]
        public async Task<bool> UpdatePlayerName(Guid characterId, string identifier, string name)
        {
            await AssertAdminAccessAsync();
            return adminManager.UpdatePlayerName(characterId, name);
        }

        [HttpGet("updateplayerskill/{characterId}/{skill}/{experience}")]
        public async Task<bool> UpdatePlayerSkill(Guid characterId, string skill, decimal experience)
        {
            await AssertAdminAccessAsync();
            return adminManager.UpdatePlayerSkill(characterId, skill, experience);
        }

        [HttpGet("kick/{characterId}")]
        public async Task<bool> KickPlayer(Guid characterId)
        {
            await AssertAdminAccessAsync();
            return adminManager.KickPlayer(characterId);
        }

        [HttpGet("suspend/{userid}")]
        public async Task<bool> SuspendPlayer(string userid)
        {
            await AssertAdminAccessAsync();
            return adminManager.SuspendPlayer(userid);
        }
        private async Task AssertAdminAccessAsync()
        {
            var authToken = GetAuthToken();
            if (authToken != null)
            {
                AssertAdminAuthToken(authToken);
                return;
            }

            var twitchUser = await sessionInfoProvider.GetTwitchUserAsync(SessionId);
            AssertAdminTwitchUser(twitchUser);
        }

        private string SessionId => SessionCookie.GetSessionId(HttpContext);

        private AuthToken GetAuthToken()
        {
            if (HttpContext.Request.Headers.TryGetValue("auth-token", out var value))
                return authManager.Get(value);
            if (sessionInfoProvider.TryGetAuthToken(SessionId, out var authToken))
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

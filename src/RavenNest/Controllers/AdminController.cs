using Microsoft.AspNetCore.Mvc;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Game;
using RavenNest.Models;
using RavenNest.Sessions;
using System;
using System.Runtime.CompilerServices;

namespace RavenNest.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AdminController : ControllerBase
    {
        private readonly IGameData gameData;
        private readonly ISessionInfoProvider sessionInfoProvider;
        private readonly IAdminManager adminManager;
        private readonly IAuthManager authManager;

        public AdminController(
            IGameData gameData,
            ISessionInfoProvider sessionInfoProvider,
            IAdminManager adminManager,
            IAuthManager authManager)
        {
            this.gameData = gameData;
            this.sessionInfoProvider = sessionInfoProvider;
            this.adminManager = adminManager;
            this.authManager = authManager;
        }

        [HttpGet("players/{offset}/{size}/{order}/{query}")]
        public PagedPlayerCollection GetPlayers(int offset, int size, string order, string query)
        {
            var authToken = GetAuthToken();
            AssertAdminAuthToken(authToken);
            return adminManager.GetPlayersPaged(offset, size, order, query);
        }

        [HttpGet("sessions/{offset}/{size}/{order}/{query}")]
        public PagedSessionCollection GetSessions(int offset, int size, string order, string query)
        {
            var authToken = GetAuthToken();
            AssertAdminAuthToken(authToken);
            return adminManager.GetSessionsPaged(offset, size, order, query);
        }

        [HttpGet("mergeplayer/{userid}")]
        public bool MergePlayerAccounts(string userid)
        {
            var authToken = GetAuthToken();
            AssertAdminAuthToken(authToken);
            return adminManager.MergePlayerAccounts(userid);
        }

        [HttpGet("updateplayername/{userid}/{name}")]
        public bool UpdatePlayerName(string userid, string name)
        {
            var authToken = GetAuthToken();
            AssertAdminAuthToken(authToken);
            return adminManager.UpdatePlayerName(userid, name);
        }

        [HttpGet("updateplayerskill/{userid}/{skill}/{experience}")]
        public bool UpdatePlayerSkill(string userid, string skill, decimal experience)
        {
            var authToken = GetAuthToken();
            AssertAdminAuthToken(authToken);
            return adminManager.UpdatePlayerSkill(userid, skill, experience);
        }

        [HttpGet("kick/{userid}")]
        public bool KickPlayer(string userid)
        {
            var authToken = GetAuthToken();
            AssertAdminAuthToken(authToken);
            return adminManager.KickPlayer(userid);
        }

        [HttpGet("suspend/{userid}")]
        public bool SuspendPlayer(string userid)
        {
            var authToken = GetAuthToken();
            AssertAdminAuthToken(authToken);
            return adminManager.SuspendPlayer(userid);
        }

        private AuthToken GetAuthToken()
        {
            if (HttpContext.Request.Headers.TryGetValue("auth-token", out var value))
            {
                return authManager.Get(value);
            }

            if (sessionInfoProvider.TryGetAuthToken(HttpContext.Session, out var authToken))
            {
                return authToken;
            }

            return null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AssertAdminAuthToken(AuthToken authToken)
        {
            var user = gameData.GetUser(authToken.UserId);
            if (!user.IsAdmin.GetValueOrDefault())
                throw new Exception("You do not have permissions to call this API");
        }
    }
}
using System;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Game;
using RavenNest.Models;
using RavenNest.Sessions;

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

        [HttpGet("players/{offset}/{size}")]
        public PagedPlayerCollection GetPlayers(int offset, int size)
        {
            var authToken = GetAuthToken();
            AssertAdminAuthToken(authToken);
            return adminManager.GetPlayersPaged(offset, size);
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
﻿using Microsoft.AspNetCore.Mvc;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Game;
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

        [HttpGet("updateplayername/{userid}/{name}")]
        public async Task<bool> UpdatePlayerName(string userid, string name)
        {
            await AssertAdminAccessAsync();
            return adminManager.UpdatePlayerName(userid, name);
        }

        [HttpGet("updateplayerskill/{userid}/{skill}/{experience}")]
        public async Task<bool> UpdatePlayerSkill(string userid, string skill, decimal experience)
        {
            await AssertAdminAccessAsync();
            return adminManager.UpdatePlayerSkill(userid, skill, experience);
        }

        [HttpGet("kick/{userid}")]
        public async Task<bool> KickPlayer(string userid)
        {
            await AssertAdminAccessAsync();
            return adminManager.KickPlayer(userid);
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

            var twitchUser = await sessionInfoProvider.GetTwitchUserAsync(HttpContext.Session);
            AssertAdminTwitchUser(twitchUser);
        }

        private AuthToken GetAuthToken()
        {
            if (HttpContext.Request.Headers.TryGetValue("auth-token", out var value))
                return authManager.Get(value);
            if (sessionInfoProvider.TryGetAuthToken(HttpContext.Session, out var authToken))
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
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
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
    public class VillageController : ControllerBase
    {
        private readonly ISessionInfoProvider sessionInfoProvider;
        private readonly ISessionManager sessionManager;
        private readonly IPlayerManager playerManager;

        private readonly ISecureHasher secureHasher;
        private readonly IAuthManager authManager;

        public VillageController(
            ISessionInfoProvider sessionInfoProvider,
            ISessionManager sessionManager,
            IPlayerManager playerManager,
            ISecureHasher secureHasher,
            IAuthManager authManager)
        {
            this.sessionInfoProvider = sessionInfoProvider;
            this.sessionManager = sessionManager;
            this.playerManager = playerManager;
            this.secureHasher = secureHasher;
            this.authManager = authManager;
        }

        [HttpGet("{slot}/assign/{userId}")]
        public Task<bool> AssignPlayerAsync(int slot, string userId)
        {
            return Task.FromResult(false);
        }

        [HttpGet("{slot}/build/{type}")]
        public Task<bool> BuildHouseAsync(int slot, int type)
        {
            return Task.FromResult(false);
        }

        [HttpGet("{slot}/remove")]
        public Task<bool> RemoveHouseAsync(int slot)
        {
            return Task.FromResult(false);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void AssertSessionTokenValidity(SessionToken sessionToken)
        {
            if (sessionToken == null) throw new NullReferenceException(nameof(sessionToken));
            if (string.IsNullOrEmpty(sessionToken.AuthToken)) throw new NullReferenceException(nameof(sessionToken.AuthToken));
            if (sessionToken.Expired) throw new Exception("Session has expired.");
        }
    }
}
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
        private readonly ISessionManager sessionManager;
        private readonly IVillageManager villageManager;


        public VillageController(
            ISessionManager sessionManager,
            IVillageManager villageManager)
        {
            this.sessionManager = sessionManager;
            this.villageManager = villageManager;
        }

        [HttpGet("{slot}/assign/{userId}")]
        public bool AssignPlayerAsync(int slot, string userId)
        {
            var sessionToken = GetSessionToken();
            AssertSessionTokenValidity(sessionToken);
            return villageManager.AssignPlayerToHouse(sessionToken.SessionId, slot, userId);
        }

        [HttpGet("{slot}/assign-character/{characterId}")]
        public bool AssignPlayerByCharacterAsync(int slot, Guid characterId)
        {
            var sessionToken = GetSessionToken();
            AssertSessionTokenValidity(sessionToken);
            return villageManager.AssignPlayerToHouse(sessionToken.SessionId, slot, characterId);
        }

        [HttpGet("{slot}/build/{type}")]
        public bool BuildHouseAsync(int slot, int type)
        {
            var sessionToken = GetSessionToken();
            AssertSessionTokenValidity(sessionToken);
            return villageManager.BuildHouse(sessionToken.SessionId, slot, type);
        }

        [HttpGet("{slot}/remove")]
        public bool RemoveHouseAsync(int slot)
        {
            var sessionToken = GetSessionToken();
            AssertSessionTokenValidity(sessionToken);
            return villageManager.RemoveHouse(sessionToken.SessionId, slot);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private SessionToken GetSessionToken()
        {
            return HttpContext.Request.Headers.TryGetValue("session-token", out var value)
                ? sessionManager.Get(value)
                : null;
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

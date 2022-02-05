using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Game;
using RavenNest.BusinessLogic.Net;
using RavenNest.Sessions;
using System;

namespace RavenNest.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class VillageController : GameApiController
    {
        private readonly IVillageManager villageManager;

        public VillageController(
            ILogger<VillageController> logger,
            IGameData gameData,
            IAuthManager authManager,
            ISessionInfoProvider sessionInfoProvider,
            ISessionManager sessionManager,
            IVillageManager villageManager,
            ISecureHasher secureHasher)
            : base(logger, gameData, authManager, sessionInfoProvider, sessionManager, secureHasher)
        {
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


        [HttpGet]
        public VillageInfo GetVillageInfo()
        {
            var sessionToken = GetSessionToken();
            AssertSessionTokenValidity(sessionToken);
            return villageManager.GetVillageInfo(sessionToken.SessionId);
        }
    }
}

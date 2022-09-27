using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Game;
using RavenNest.BusinessLogic.Net;
using RavenNest.Models;
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
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("{slot}/assign/{userId}")]
        public bool AssignPlayerAsync(int slot, string userId)
        {
            var sessionToken = GetSessionToken();
            AssertSessionTokenValidity(sessionToken);
            return villageManager.AssignPlayerToHouse(sessionToken.SessionId, slot, userId);
        }
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("assign-village")]
        public bool AssignVillage([FromBody] VillageAssignRequest request)
        {
            var sessionToken = GetSessionToken();
            AssertSessionTokenValidity(sessionToken);
            return villageManager.AssignVillage(sessionToken.SessionId, request.Type, request.CharacterIds);
        }
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("{slot}/assign-character/{characterId}")]
        public bool AssignPlayerByCharacterAsync(int slot, Guid characterId)
        {
            var sessionToken = GetSessionToken();
            AssertSessionTokenValidity(sessionToken);
            return villageManager.AssignPlayerToHouse(sessionToken.SessionId, slot, characterId);
        }
        [ApiExplorerSettings(IgnoreApi = true)]

        [HttpGet("{slot}/build/{type}")]
        public bool BuildHouseAsync(int slot, int type)
        {
            var sessionToken = GetSessionToken();
            AssertSessionTokenValidity(sessionToken);
            return villageManager.BuildHouse(sessionToken.SessionId, slot, type);
        }
        [ApiExplorerSettings(IgnoreApi = true)]

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

    public class VillageAssignRequest
    {
        public int Type { get; set; }
        public Guid[] CharacterIds { get; set; }
    }
}

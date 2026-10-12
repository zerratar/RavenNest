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
        private readonly VillageManager villageManager;

        public VillageController(
            ILogger<VillageController> logger,
            GameData gameData,
            IAuthManager authManager,
            SessionInfoProvider sessionInfoProvider,
            SessionManager sessionManager,
            VillageManager villageManager,
            ISecureHasher secureHasher)
            : base(logger, gameData, authManager, sessionInfoProvider, sessionManager, secureHasher)
        {
            this.villageManager = villageManager;
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

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("placements")]
        public VillageHousePlacement[] GetPlacements()
        {
            var sessionToken = GetSessionToken();
            AssertSessionTokenValidity(sessionToken);
            return villageManager.GetPlacements(sessionToken.SessionId);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("{slot}/place")]
        public bool PlaceHouse(int slot, [FromBody] VillageHousePlacement request)
        {
            var sessionToken = GetSessionToken();
            AssertSessionTokenValidity(sessionToken);
            return request != null && villageManager.PlaceHouse(sessionToken.SessionId, slot, request.X, request.Z, request.Rotation);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("{slot}/reset-placement")]
        public bool ResetHousePlacement(int slot)
        {
            var sessionToken = GetSessionToken();
            AssertSessionTokenValidity(sessionToken);
            return villageManager.ResetHousePlacement(sessionToken.SessionId, slot);
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

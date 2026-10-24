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
        private readonly VillageRoadManager roadManager;
        private readonly VillageDecorManager decorManager;

        public VillageController(
            ILogger<VillageController> logger,
            GameData gameData,
            IAuthManager authManager,
            SessionInfoProvider sessionInfoProvider,
            SessionManager sessionManager,
            VillageManager villageManager,
            VillageRoadManager roadManager,
            VillageDecorManager decorManager,
            ISecureHasher secureHasher)
            : base(logger, gameData, authManager, sessionInfoProvider, sessionManager, secureHasher)
        {
            this.villageManager = villageManager;
            this.roadManager = roadManager;
            this.decorManager = decorManager;
        }

        // ---- Decor, placed in build mode. See VillageDecorManager. ----

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("decor/pieces")]
        public VillageDecorPiece[] GetDecorPieces()
        {
            var sessionToken = GetSessionToken();
            AssertSessionTokenValidity(sessionToken);
            return decorManager.GetPieces(sessionToken.SessionId);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("decor")]
        public VillageDecorInfo[] GetDecor()
        {
            var sessionToken = GetSessionToken();
            AssertSessionTokenValidity(sessionToken);
            return decorManager.GetDecor(sessionToken.SessionId);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("decor/place")]
        public VillageDecorResult PlaceDecor([FromBody] VillageDecorRequest request)
        {
            var sessionToken = GetSessionToken();
            AssertSessionTokenValidity(sessionToken);
            return decorManager.Place(sessionToken.SessionId, request);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("decor/move")]
        public VillageDecorResult MoveDecor([FromBody] VillageDecorRequest request)
        {
            var sessionToken = GetSessionToken();
            AssertSessionTokenValidity(sessionToken);
            return decorManager.Move(sessionToken.SessionId, request);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("decor/remove")]
        public VillageDecorResult RemoveDecor([FromBody] VillageDecorRequest request)
        {
            var sessionToken = GetSessionToken();
            AssertSessionTokenValidity(sessionToken);
            return decorManager.Remove(sessionToken.SessionId, request);
        }

        // ---- Roads, drawn in build mode. See VillageRoadManager. ----

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("roads/types")]
        public VillageRoadType[] GetRoadTypes()
        {
            var sessionToken = GetSessionToken();
            AssertSessionTokenValidity(sessionToken);
            return roadManager.GetTypes(sessionToken.SessionId);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("roads")]
        public VillageRoadInfo[] GetRoads()
        {
            var sessionToken = GetSessionToken();
            AssertSessionTokenValidity(sessionToken);
            return roadManager.GetRoads(sessionToken.SessionId);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("roads/build")]
        public VillageRoadResult BuildRoad([FromBody] VillageRoadRequest request)
        {
            var sessionToken = GetSessionToken();
            AssertSessionTokenValidity(sessionToken);
            return roadManager.Build(sessionToken.SessionId, request);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("roads/{id}/change")]
        public VillageRoadResult ChangeRoad(Guid id, [FromBody] VillageRoadRequest request)
        {
            var sessionToken = GetSessionToken();
            AssertSessionTokenValidity(sessionToken);
            return roadManager.Change(sessionToken.SessionId, id, request);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("roads/{id}/remove")]
        public VillageRoadResult RemoveRoad(Guid id)
        {
            var sessionToken = GetSessionToken();
            AssertSessionTokenValidity(sessionToken);
            return roadManager.Remove(sessionToken.SessionId, id);
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
        [HttpGet("townhall")]
        public VillageTownHall GetTownHall()
        {
            var sessionToken = GetSessionToken();
            AssertSessionTokenValidity(sessionToken);
            return villageManager.GetTownHall(sessionToken.SessionId);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("townhall/place")]
        public bool PlaceTownHall([FromBody] VillageTownHallPlacement request)
        {
            var sessionToken = GetSessionToken();
            AssertSessionTokenValidity(sessionToken);
            return request != null && villageManager.PlaceTownHall(sessionToken.SessionId, request.X, request.Z, request.Rotation);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("townhall/upgrade")]
        public VillageTownHall UpgradeTownHall()
        {
            var sessionToken = GetSessionToken();
            AssertSessionTokenValidity(sessionToken);
            return villageManager.UpgradeTownHall(sessionToken.SessionId);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("townhall/reset-placement")]
        public bool ResetTownHallPlacement()
        {
            var sessionToken = GetSessionToken();
            AssertSessionTokenValidity(sessionToken);
            return villageManager.ResetTownHallPlacement(sessionToken.SessionId);
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

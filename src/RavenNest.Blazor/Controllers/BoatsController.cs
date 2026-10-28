using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Game;
using RavenNest.BusinessLogic.Net;
using RavenNest.Models;
using RavenNest.Sessions;

namespace RavenNest.Controllers
{
    /// <summary>Own boats (docs/own-boats.md in the game): the shipwright's boats and !boat build.</summary>
    [Route("api/[controller]")]
    [ApiController]
    public class BoatsController : GameApiController
    {
        private readonly BoatManager boatManager;

        public BoatsController(
            ILogger<BoatsController> logger,
            GameData gameData,
            IAuthManager authManager,
            SessionInfoProvider sessionInfoProvider,
            SessionManager sessionManager,
            BoatManager boatManager,
            ISecureHasher secureHasher)
            : base(logger, gameData, authManager, sessionInfoProvider, sessionManager, secureHasher)
        {
            this.boatManager = boatManager;
        }

        /// <summary>What the shipwright builds: levels, parts and fee per boat.</summary>
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("blueprints")]
        public BoatBlueprint[] GetBlueprints()
        {
            var sessionToken = GetSessionToken();
            AssertSessionTokenValidity(sessionToken);
            return boatManager.GetBlueprints();
        }

        /// <summary>!boat build: the character, on Away, has the shipwright build a boat from its parts and coins.</summary>
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("{characterId}/build")]
        public BoatBuildResult Build(Guid characterId, [FromBody] BoatBuildRequest request)
        {
            var sessionToken = GetSessionToken();
            AssertSessionTokenValidity(sessionToken);
            return boatManager.Build(sessionToken, characterId, request?.Boat);
        }

        /// <summary>A guest climbed aboard the owner's boat (the game's "Guests aboard").</summary>
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("{ownerId}/guest-aboard/{guestId}")]
        public bool GuestAboard(Guid ownerId, Guid guestId)
        {
            var sessionToken = GetSessionToken();
            AssertSessionTokenValidity(sessionToken);
            return boatManager.GuestAboard(sessionToken, ownerId, guestId);
        }

        /// <summary>A guest stepped off; "trip" when they sailed a fair way, for the owner's Ferryman titles.</summary>
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("{ownerId}/guest-ashore/{guestId}/{trip}")]
        public bool GuestAshore(Guid ownerId, Guid guestId, string trip)
        {
            var sessionToken = GetSessionToken();
            AssertSessionTokenValidity(sessionToken);
            return boatManager.GuestAshore(sessionToken, ownerId, guestId, trip == "trip");
        }
    }
}

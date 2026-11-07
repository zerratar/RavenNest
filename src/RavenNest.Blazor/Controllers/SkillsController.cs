using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Game;
using RavenNest.BusinessLogic.Net;
using RavenNest.Models;
using RavenNest.Sessions;

namespace RavenNest.Controllers
{
    /// <summary>
    ///     The skill trees for the game (combat v2, the game's docs/combat-skills-design.md): !skills, !learn, !loadout and
    ///     !respec. Only for characters in the calling game's session.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class SkillsController : GameApiController
    {
        private readonly SkillTreeManager skillTrees;

        public SkillsController(
            ILogger<SkillsController> logger,
            GameData gameData,
            IAuthManager authManager,
            SessionInfoProvider sessionInfoProvider,
            SessionManager sessionManager,
            SkillTreeManager skillTrees,
            ISecureHasher secureHasher)
            : base(logger, gameData, authManager, sessionInfoProvider, sessionManager, secureHasher)
        {
            this.skillTrees = skillTrees;
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("{characterId}")]
        public SkillBuildInfo Get(Guid characterId)
        {
            return InSession(characterId) ? skillTrees.Get(characterId) : null;
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("{characterId}/learn/{node}")]
        public SkillBuildResult Learn(Guid characterId, string node)
        {
            return InSession(characterId) ? skillTrees.Raise(characterId, node) : NotInSession();
        }

        /// <summary>Puts an active in a slot (1 based); "-" empties it.</summary>
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("{characterId}/loadout/{slot}/{node}")]
        public SkillBuildResult Loadout(Guid characterId, int slot, string node)
        {
            return InSession(characterId) ? skillTrees.SetLoadout(characterId, slot, node) : NotInSession();
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("{characterId}/respec")]
        public SkillBuildResult Respec(Guid characterId)
        {
            return InSession(characterId) ? skillTrees.Respec(characterId) : NotInSession();
        }

        private bool InSession(Guid characterId)
        {
            var token = GetSessionToken();
            AssertSessionTokenValidity(token);
            var session = GameData.GetSession(token.SessionId);
            var characters = session != null ? GameData.GetActiveSessionCharacters(session) : null;
            return characters != null && characters.Any(x => x.Id == characterId);
        }

        private static SkillBuildResult NotInSession() => new SkillBuildResult { Message = "That character is not playing on this stream." };
    }
}

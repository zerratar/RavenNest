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
    /// <summary>A character's quests, for the game the character plays in (!quest). See QuestManager.</summary>
    [Route("api/[controller]")]
    [ApiController]
    public class QuestsController : GameApiController
    {
        private readonly QuestManager questManager;

        public QuestsController(
            ILogger<QuestsController> logger,
            GameData gameData,
            IAuthManager authManager,
            SessionInfoProvider sessionInfoProvider,
            SessionManager sessionManager,
            QuestManager questManager,
            ISecureHasher secureHasher)
            : base(logger, gameData, authManager, sessionInfoProvider, sessionManager, secureHasher)
        {
            this.questManager = questManager;
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("{characterId}")]
        public QuestList GetQuests(Guid characterId)
        {
            var sessionToken = GetSessionToken();
            AssertSessionTokenValidity(sessionToken);
            return questManager.GetQuests(sessionToken, characterId);
        }

        /// <summary>Takes one of today's offers (slot 0 to 2), or this week's quest (slot -1).</summary>
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("{characterId}/accept/{slot}")]
        public QuestActionResult Accept(Guid characterId, int slot)
        {
            var sessionToken = GetSessionToken();
            AssertSessionTokenValidity(sessionToken);
            return questManager.Accept(sessionToken, characterId, slot);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("{characterId}/abandon/{questId}")]
        public QuestActionResult Abandon(Guid characterId, Guid questId)
        {
            var sessionToken = GetSessionToken();
            AssertSessionTokenValidity(sessionToken);
            return questManager.Abandon(sessionToken, characterId, questId);
        }
    }
}

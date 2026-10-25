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
        private readonly StreamQuestManager streamQuestManager;

        public QuestsController(
            ILogger<QuestsController> logger,
            GameData gameData,
            IAuthManager authManager,
            SessionInfoProvider sessionInfoProvider,
            SessionManager sessionManager,
            QuestManager questManager,
            StreamQuestManager streamQuestManager,
            ISecureHasher secureHasher)
            : base(logger, gameData, authManager, sessionInfoProvider, sessionManager, secureHasher)
        {
            this.questManager = questManager;
            this.streamQuestManager = streamQuestManager;
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

        /// <summary>The stream's quest: the running one and today's three offers. See StreamQuestManager.</summary>
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("stream")]
        public StreamQuestState GetStreamQuest()
        {
            var sessionToken = GetSessionToken();
            AssertSessionTokenValidity(sessionToken);
            return streamQuestManager.GetState(sessionToken);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("stream/start/{slot}")]
        public StreamQuestResult StartStreamQuest(int slot)
        {
            var sessionToken = GetSessionToken();
            AssertSessionTokenValidity(sessionToken);
            return streamQuestManager.Start(sessionToken, slot);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("stream/stop")]
        public StreamQuestResult StopStreamQuest()
        {
            var sessionToken = GetSessionToken();
            AssertSessionTokenValidity(sessionToken);
            return streamQuestManager.Stop(sessionToken);
        }
    }
}

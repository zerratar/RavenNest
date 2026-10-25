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
        private readonly AchievementManager achievementManager;

        public QuestsController(
            ILogger<QuestsController> logger,
            GameData gameData,
            IAuthManager authManager,
            SessionInfoProvider sessionInfoProvider,
            SessionManager sessionManager,
            QuestManager questManager,
            StreamQuestManager streamQuestManager,
            AchievementManager achievementManager,
            ISecureHasher secureHasher)
            : base(logger, gameData, authManager, sessionInfoProvider, sessionManager, secureHasher)
        {
            this.questManager = questManager;
            this.streamQuestManager = streamQuestManager;
            this.achievementManager = achievementManager;
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

        /// <summary>Begins a quest chain ("cat", "fish", ...).</summary>
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("{characterId}/chain/{key}")]
        public QuestActionResult BeginChain(Guid characterId, string key)
        {
            var sessionToken = GetSessionToken();
            AssertSessionTokenValidity(sessionToken);
            return questManager.BeginChain(sessionToken, characterId, key);
        }

        /// <summary>Hands in the items the chain's delivery step asks for.</summary>
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("{characterId}/deliver")]
        public QuestActionResult Deliver(Guid characterId)
        {
            var sessionToken = GetSessionToken();
            AssertSessionTokenValidity(sessionToken);
            return questManager.DeliverItems(sessionToken, characterId);
        }

        /// <summary>Answers the chain's riddle or choice (from 1).</summary>
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("{characterId}/answer/{answer}")]
        public QuestActionResult Answer(Guid characterId, int answer)
        {
            var sessionToken = GetSessionToken();
            AssertSessionTokenValidity(sessionToken);
            return questManager.AnswerStep(sessionToken, characterId, answer);
        }

        /// <summary>The character's achievements, the titles earned and the one worn. See AchievementManager.</summary>
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("{characterId}/achievements")]
        public AchievementList GetAchievements(Guid characterId)
        {
            var sessionToken = GetSessionToken();
            AssertSessionTokenValidity(sessionToken);
            return achievementManager.GetAchievements(sessionToken, characterId);
        }

        /// <summary>Wears an earned title ("none" for none).</summary>
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("{characterId}/title/{title}")]
        public TitleResult SetTitle(Guid characterId, string title)
        {
            var sessionToken = GetSessionToken();
            AssertSessionTokenValidity(sessionToken);
            return achievementManager.SetTitle(sessionToken, characterId, title);
        }

        /// <summary>The titles worn in this stream, for the name tags.</summary>
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("titles")]
        public SessionTitles GetTitles()
        {
            var sessionToken = GetSessionToken();
            AssertSessionTokenValidity(sessionToken);
            return achievementManager.GetSessionTitles(sessionToken);
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

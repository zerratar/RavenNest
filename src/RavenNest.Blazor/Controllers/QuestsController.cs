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
        private readonly StreamerQuestManager streamerQuestManager;
        private readonly VillageStoryManager storyManager;
        private readonly HeroModeManager heroManager;

        public QuestsController(
            ILogger<QuestsController> logger,
            GameData gameData,
            IAuthManager authManager,
            SessionInfoProvider sessionInfoProvider,
            SessionManager sessionManager,
            QuestManager questManager,
            StreamQuestManager streamQuestManager,
            AchievementManager achievementManager,
            StreamerQuestManager streamerQuestManager,
            VillageStoryManager storyManager,
            HeroModeManager heroManager,
            ISecureHasher secureHasher)
            : base(logger, gameData, authManager, sessionInfoProvider, sessionManager, secureHasher)
        {
            this.questManager = questManager;
            this.streamQuestManager = streamQuestManager;
            this.achievementManager = achievementManager;
            this.streamerQuestManager = streamerQuestManager;
            this.storyManager = storyManager;
            this.heroManager = heroManager;
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

        /// <summary>The stream's streamer quests, and what the streamer can set aside. See StreamerQuestManager.</summary>
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("streamer")]
        public StreamerQuestList GetStreamerQuests()
        {
            var sessionToken = GetSessionToken();
            AssertSessionTokenValidity(sessionToken);
            return streamerQuestManager.GetList(sessionToken);
        }

        /// <summary>The running streamer quests with one character's progress (!quest).</summary>
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("streamer/{characterId}")]
        public StreamerQuestInfo[] GetStreamerQuestsFor(Guid characterId)
        {
            var sessionToken = GetSessionToken();
            AssertSessionTokenValidity(sessionToken);
            return streamerQuestManager.GetFor(sessionToken, characterId);
        }

        /// <summary>Posts a streamer quest, setting its reward aside.</summary>
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("streamer/post")]
        public StreamerQuestResult PostStreamerQuest([FromBody] StreamerQuestRequest request)
        {
            var sessionToken = GetSessionToken();
            AssertSessionTokenValidity(sessionToken);
            return streamerQuestManager.Post(sessionToken, request);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("streamer/stop/{questId}")]
        public StreamerQuestResult StopStreamerQuest(Guid questId)
        {
            var sessionToken = GetSessionToken();
            AssertSessionTokenValidity(sessionToken);
            return streamerQuestManager.Stop(sessionToken, questId);
        }

        /// <summary>The streamer's story lines and unlocks. See VillageStoryManager.</summary>
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("village")]
        public VillageLog GetVillageLog()
        {
            var sessionToken = GetSessionToken();
            AssertSessionTokenValidity(sessionToken);
            return storyManager.GetLog(sessionToken);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("village/begin/{key}")]
        public VillageStoryResult BeginStory(string key)
        {
            var sessionToken = GetSessionToken();
            AssertSessionTokenValidity(sessionToken);
            return storyManager.Begin(sessionToken, key);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("village/talk/{key}")]
        public VillageStoryResult TalkStory(string key)
        {
            var sessionToken = GetSessionToken();
            AssertSessionTokenValidity(sessionToken);
            return storyManager.Talk(sessionToken, key);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("village/choose/{key}/{option}")]
        public VillageStoryResult ChooseStory(string key, int option)
        {
            var sessionToken = GetSessionToken();
            AssertSessionTokenValidity(sessionToken);
            return storyManager.Choose(sessionToken, key, option);
        }

        /// <summary>A viewer brings the story's delivery item from their inventory (!quest give).</summary>
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("village/give/{characterId}")]
        public VillageStoryResult GiveStory(Guid characterId)
        {
            var sessionToken = GetSessionToken();
            AssertSessionTokenValidity(sessionToken);
            return storyManager.Give(sessionToken, characterId);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("village/abandon/{key}")]
        public VillageStoryResult AbandonStory(string key)
        {
            var sessionToken = GetSessionToken();
            AssertSessionTokenValidity(sessionToken);
            return storyManager.Abandon(sessionToken, key);
        }

        /// <summary>The streamer's character finished the Ravebridge island quests: the title "of Ravebridge".</summary>
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("village/ravebridge-done")]
        public VillageStoryResult RavebridgeQuestsDone()
        {
            var sessionToken = GetSessionToken();
            AssertSessionTokenValidity(sessionToken);
            return storyManager.RavebridgeQuestsDone(sessionToken);
        }

        /// <summary>A hero mode wave run ended: the streamer's best and the titles of waves 10, 25 and 50.</summary>
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("hero/run/{waves}/{kills}/{seconds}")]
        public HeroRunResult HeroRun(int waves, int kills, int seconds)
        {
            var sessionToken = GetSessionToken();
            AssertSessionTokenValidity(sessionToken);
            return heroManager.Record(sessionToken, waves, kills, seconds);
        }

        /// <summary>Every streamer's best hero mode run, the best first.</summary>
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("hero/leaderboard")]
        public HeroLeaderboard HeroLeaderboard()
        {
            return heroManager.Leaderboard(10);
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

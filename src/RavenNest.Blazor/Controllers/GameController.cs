using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Game;
using RavenNest.Models;
using System;
using System.Threading.Tasks;

namespace RavenNest.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    //[ApiDescriptor(Name = "Game API", Description = "Used for handling game sessions and polling game events.")]
    public class GameController : GameApiController
    {
        private readonly GameData gameData;
        private readonly AdminManager adminManager;
        private readonly SessionManager sessionManager;
        private readonly GameManager gameManager;
        private readonly SessionInfoProvider siProvider;
        private readonly ILogger<GameController> logger;

        public GameController(
            ILogger<GameController> logger,
            GameData gameData,
            AdminManager adminManager,
            IAuthManager authManager,
            SessionInfoProvider sessionInfoProvider,
            SessionManager sessionManager,
            GameManager gameManager,
            ISecureHasher secureHasher)
            : base(logger, gameData, authManager, sessionInfoProvider, sessionManager, secureHasher)
        {
            this.siProvider = sessionInfoProvider;
            this.logger = logger;
            this.gameData = gameData;
            this.adminManager = adminManager;
            this.sessionManager = sessionManager;
            this.gameManager = gameManager;
        }

        [HttpGet]
        public GameInfo Get()
        {
            var session = GetSessionToken();
            AssertSessionTokenValidity(session);
            return gameManager.GetGameInfo(session);
        }

        [HttpGet("exp-multiplier")]
        public ExpMultiplier GetExpMultiplier()
        {
            var activeEvent = gameData.GetActiveExpMultiplierEvent();
            if (activeEvent == null)
                return new ExpMultiplier();

            return new ExpMultiplier
            {
                EndTime = activeEvent.EndTime,
                EventName = activeEvent.EventName,
                StartedByPlayer = activeEvent.StartedByPlayer,
                Multiplier = activeEvent.Multiplier,
                StartTime = activeEvent.StartTime
            };
        }

        [HttpGet("state-data")]
        public async Task<ActionResult> DownloadStreamerStateCache()
        {
            try
            {
                var sessionId = HttpContext.GetSessionId();
                if (!siProvider.TryGet(sessionId, out var si))
                {
                    return Unauthorized();
                }

                var cache = adminManager.GetStreamerStateCache(si.UserId);
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(cache);
                var fileContent = System.Text.UTF8Encoding.UTF8.GetBytes(json);
                return File(fileContent, "application/json", "state-data.json", true);
            }
            catch
            {
                return NotFound();
            }
        }
        [HttpPost("report-exception")]
        public async Task<bool> ReportExceptionAsync([FromBody] string error)
        {
            try
            {
                var sessionUserName = "client";
                var authToken = GetAuthToken();
                if (authToken != null)
                {
                    var user = gameData.GetUser(authToken.UserId);
                    sessionUserName = user.UserName;
                }

                logger.LogError("Exception Reported by " + sessionUserName + ": " + error);
                return true;
            }
            catch
            {
                return false;
            }
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("{clientVersion}/{accessKey}")]
        public async Task<SessionToken> BeginSessionAsync(string clientVersion, string accessKey, Two<bool, float> param)
        {
            var authToken = GetAuthToken();
            AssertAuthTokenValidity(authToken);

            var session = await this.sessionManager.BeginSessionAsync(
                authToken,
                clientVersion,
                accessKey,
                param.Value1,
                param.Value2);

            if (session != null && session.AuthToken == null)
            {
                return null;
            }

            if (session == null)
            {
                HttpContext.Response.StatusCode = 403;
                return null;
            }

            return session;
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("{clientVersion}/{accessKey}/{gameTime}/skip-update")]
        public async Task<BeginSessionResult> BeginSessionSkipUpdateAsync(string clientVersion, string accessKey, float gameTime)
        {
            var authToken = GetAuthToken();
            AssertAuthTokenValidity(authToken);

            BeginSessionResult session = await this.sessionManager.BeginSessionAsync(
                authToken,
                clientVersion,
                accessKey,
                gameTime,
                true);

            if (session == null)
            {
                HttpContext.Response.StatusCode = 403;
                return null;
            }

            if (session.SessionToken?.AuthToken == null)
            {
                return null;
            }


            return session;
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("{clientVersion}/{accessKey}/{gameTime}")]
        public async Task<BeginSessionResult> BeginSessionAsync(string clientVersion, string accessKey, float gameTime)
        {
            var authToken = GetAuthToken();
            AssertAuthTokenValidity(authToken);

            BeginSessionResult session = await this.sessionManager.BeginSessionAsync(
                authToken,
                clientVersion,
                accessKey,
                gameTime);

            if (session == null)
            {
                HttpContext.Response.StatusCode = 403;
                return null;
            }

            if (session.SessionToken?.AuthToken == null)
            {
                return null;
            }


            return session;
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpDelete("raid/{username}")]
        public bool EndSessionAndRaid(string username, Single<bool> war)
        {
            var session = GetSessionToken();
            AssertSessionTokenValidity(session);
            return this.sessionManager.EndSessionAndRaid(session, username, war.Value);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("raid/{username}")]
        public bool PostEndSessionAndRaid(string username, Single<bool> war)
        {
            var session = GetSessionToken();
            AssertSessionTokenValidity(session);
            return this.sessionManager.EndSessionAndRaid(session, username, war.Value);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("raid/{username}/{war}")]
        public bool GetEndSessionAndRaid(string username, bool war)
        {
            try
            {
                var session = GetSessionToken();
                if (session == null)
                {
                    var errorMessage = "GetEndSessionAndRaid Error: Session is null trying to Raid " + username + ", war: " + war;
#if DEBUG
                    errorMessage += " " + GetHeaderValues();
#endif

                    logger.LogError(errorMessage);
                    return false;
                }

                AssertSessionTokenValidity(session);
                return this.sessionManager.EndSessionAndRaid(session, username, war);
            }
            catch (Exception exc)
            {
                logger.LogError(exc.ToString());
                return false;
            }
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("end")]
        public void GetEndSession()
        {
            var session = GetSessionToken();
            AssertSessionTokenValidity(session);
            this.sessionManager.EndSession(session);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost]
        public void PostEndSession()
        {
            var session = GetSessionToken();
            AssertSessionTokenValidity(session);
            this.sessionManager.EndSession(session);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpDelete]
        public void EndSession()
        {
            var session = GetSessionToken();
            AssertSessionTokenValidity(session);
            this.sessionManager.EndSession(session);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("attach")]
        public bool AttachPlayers(Many<Guid> characterIds)
        {
            try
            {
                var session = GetSessionToken();
                AssertSessionTokenValidity(session);
                return sessionManager.AttachPlayersToSession(session, characterIds.Values);
            }
            catch (Exception exc)
            {
                logger.LogError(exc.ToString());
                return false;
            }
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("{tier}/dungeon-reward")]
        public EventItemReward[] GetDungeonRewardsAsync(int tier, Many<Guid> characterIds)
        {
            try
            {
                var session = GetSessionToken();
                AssertSessionTokenValidity(session);
                return gameManager.GetDungeonRewardsAsync(session, tier, characterIds.Values);
            }
            catch (Exception exc)
            {
                logger.LogError(exc.ToString());
                return null;
            }
        }


        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("raid-reward")]
        public EventItemReward[] GetRaidRewardsAsync(Many<Guid> characterIds)
        {
            try
            {
                var session = GetSessionToken();
                AssertSessionTokenValidity(session);
                return gameManager.GetRaidRewardsAsync(session, characterIds.Values);
            }
            catch (Exception exc)
            {
                logger.LogError(exc.ToString());
                return null;
            }
        }

        [HttpGet("clear-players")]
        public bool ClearPlayers()
        {
            var session = GetSessionToken();
            AssertSessionTokenValidity(session);
            return gameManager.ClearPlayers(session);
        }

        [HttpGet("get-scrolls/{characterId}")]
        public ScrollInfoCollection GetScrolls(Guid characterId)
        {
            var session = GetSessionToken();
            AssertSessionTokenValidity(session);
            return gameManager.GetScrolls(session, characterId);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("use-exp-scroll/{characterId}/{count}")]
        public int UseExpScroll(Guid characterId, int count)
        {
            var session = GetSessionToken();
            AssertSessionTokenValidity(session);
            if (count <= 0) count = 1;
            return gameManager.UseExpScroll(session, characterId, count).Used;
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("use-exp-scroll-new/{characterId}/{count}")]
        public UseExpScrollResult UseExpScrollNew(Guid characterId, int count)
        {
            var session = GetSessionToken();
            AssertSessionTokenValidity(session);
            if (count <= 0) count = 1;
            return gameManager.UseExpScroll(session, characterId, count);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("use-scroll/{characterId}/{scrollType}")]
        public ScrollUseResult UseScroll(Guid characterId, string scrollType)
        {
            var st = ScrollType.Raid;

            if (string.IsNullOrEmpty(scrollType))
            {
                logger.LogError("Unable to use scroll: " + characterId + " / " + scrollType);
                return ScrollUseResult.Error;
            }

            if (int.TryParse(scrollType, out var ist))
            {
                st = (ScrollType)ist;
            }

            if (scrollType.ToLower().Contains("exp"))
                st = ScrollType.Experience;
            else if (scrollType.ToLower().Contains("raid"))
                st = ScrollType.Raid;
            else if (scrollType.ToLower().Contains("dungeon"))
                st = ScrollType.Dungeon;
            else if (scrollType.ToLower().Contains("ferry"))
                st = ScrollType.Ferry;


            var session = GetSessionToken();
            AssertSessionTokenValidity(session);

            return gameManager.UseScroll(session, characterId, st);
        }
    }
}

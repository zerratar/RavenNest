using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Game;
using RavenNest.Models;
using System;
using System.IO;
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
        private readonly BusinessLogic.Net.V2.GameConnectionTokens connectionTokens;

        public GameController(
            ILogger<GameController> logger,
            GameData gameData,
            AdminManager adminManager,
            IAuthManager authManager,
            SessionInfoProvider sessionInfoProvider,
            SessionManager sessionManager,
            GameManager gameManager,
            ISecureHasher secureHasher,
            BusinessLogic.Net.V2.GameConnectionTokens connectionTokens)
            : base(logger, gameData, authManager, sessionInfoProvider, sessionManager, secureHasher)
        {
            this.siProvider = sessionInfoProvider;
            this.logger = logger;
            this.gameData = gameData;
            this.adminManager = adminManager;
            this.sessionManager = sessionManager;
            this.gameManager = gameManager;
            this.connectionTokens = connectionTokens;
        }

        [HttpGet]
        public GameInfo Get()
        {
            var session = GetSessionToken();
            AssertSessionTokenValidity(session);
            return gameManager.GetGameInfo(session);
        }

        /// <summary>
        /// The signed token for the v2 game connection, for the session in the session-token header. The same
        /// value BeginSession returns as ConnectionToken, for games whose models do not have that field.
        /// </summary>
        [HttpGet("connection-token")]
        public IActionResult GetConnectionToken()
        {
            var session = GetSessionToken();
            AssertSessionTokenValidity(session);
            // as a JSON string, which the game's JSON reader expects (a plain string would go out as text)
            return new JsonResult(connectionTokens.Issue(session));
        }

        [HttpPost("upload-log/{requestId}")]
        public async Task<IActionResult> UploadLogAsync(Guid requestId)
        {
            try
            {
                var session = GetSessionToken();
                if (session == null)
                {
                    return Unauthorized("Session is not valid or does not exist.");
                }

                var user = gameData.GetUser(session.UserId);
                if (user == null)
                {
                    return NotFound("User not found.");
                }

                using var ms = new MemoryStream();
                await Request.Body.CopyToAsync(ms);
                var logContent = ms.ToArray();

                await adminManager.UploadLogAsync(requestId, session.SessionId, user, logContent);
                return Ok("Log uploaded successfully.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error uploading log");
                return StatusCode(500, "Internal server error while uploading log.");
            }
        }

        [HttpPost("upload-state/{requestId}")]
        public async Task<IActionResult> UploadStateAsync(Guid requestId)
        {
            try
            {
                var session = GetSessionToken();
                if (session == null)
                {
                    return Unauthorized("Session is not valid or does not exist.");
                }

                var user = gameData.GetUser(session.UserId);
                if (user == null)
                {
                    return NotFound("User not found.");
                }

                using var ms = new MemoryStream();
                await Request.Body.CopyToAsync(ms);
                var logContent = ms.ToArray();

                await adminManager.UploadGameStateAsync(requestId, session.SessionId, user, logContent);
                return Ok("Game state uploaded successfully.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error uploading game state");
                return StatusCode(500, "Internal server error while uploading game state.");
            }
        }

        /// <summary>
        /// Who the server remembers in the streamer's stream, in the game's state file format: the characters locked
        /// to the streamer and the last player list the game sent. The game compares it with its own state file at
        /// startup, so players are not lost with a lost state file (a reinstall).
        /// </summary>
        [HttpGet("session-players")]
        public GameSessionPlayerCache GetSessionPlayers()
        {
            var session = GetSessionToken();
            AssertSessionTokenValidity(session);
            var cache = adminManager.GetStreamerStateCache(session.UserId);
            // when the list was last stored, so the game's state expiry setting applies to it as to its own file
            var stored = sessionManager.GetSessionPlayerListTime(session.UserId);
            if (stored != null) cache.Created = stored.Value;
            return cache;
        }

        /// <summary>
        /// Players the streamer chose not to restore: no longer locked to the streamer, so the server stops offering them
        /// at the next start. Only characters locked to this streamer are touched.
        /// </summary>
        [HttpPost("release-players")]
        public int ReleasePlayers([FromBody] Guid[] characterIds)
        {
            var session = GetSessionToken();
            AssertSessionTokenValidity(session);
            var released = 0;
            foreach (var id in characterIds ?? Array.Empty<Guid>())
            {
                var character = gameData.GetCharacter(id);
                if (character == null || character.UserIdLock != session.UserId) continue;
                character.PrevUserIdLock = character.UserIdLock;
                character.UserIdLock = null;
                released++;
            }
            return released;
        }

        /// <summary>The players in the streamer's game now, sent by the game about once a minute; replaces the last list.</summary>
        [HttpPost("session-players")]
        public bool SetSessionPlayers([FromBody] Guid[] characterIds)
        {
            var session = GetSessionToken();
            AssertSessionTokenValidity(session);
            return sessionManager.SetSessionPlayerList(session.UserId, characterIds);
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

            session.ConnectionToken = connectionTokens.Issue(session.SessionToken);
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

            session.ConnectionToken = connectionTokens.Issue(session.SessionToken);
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

        /// <summary>Hero mode's reward chest after a wave run: the hero first, then the top helpers.</summary>
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("{waves}/hero-chest")]
        public EventItemReward[] GetHeroChest(int waves, Many<Guid> characterIds)
        {
            try
            {
                var session = GetSessionToken();
                AssertSessionTokenValidity(session);
                return gameManager.GetHeroChest(session, waves, characterIds.Values);
            }
            catch (Exception exc)
            {
                logger.LogError(exc.ToString());
                return null;
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

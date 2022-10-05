using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Game;
using RavenNest.BusinessLogic.Net;
using RavenNest.Models;
using RavenNest.Sessions;

namespace RavenNest.Controllers
{

    [Route("api/[controller]")]
    [ApiController]
    //[ApiDescriptor(Name = "Game API", Description = "Used for handling game sessions and polling game events.")]
    public class GameController : GameApiController
    {
        private readonly IGameData gameData;
        private readonly IAuthManager authManager;
        private readonly ISessionInfoProvider sessionInfoProvider;
        private readonly ISessionManager sessionManager;
        private readonly IGameManager gameManager;
        private readonly ISecureHasher secureHasher;
        private readonly ILogger<GameController> logger;

        public GameController(
            ILogger<GameController> logger,
            IGameData gameData,
            IAuthManager authManager,
            ISessionInfoProvider sessionInfoProvider,
            ISessionManager sessionManager,
            IGameManager gameManager,
            ISecureHasher secureHasher)
            : base(logger, gameData, authManager, sessionInfoProvider, sessionManager, secureHasher)
        {
            this.logger = logger;
            this.gameData = gameData;
            this.authManager = authManager;
            this.sessionInfoProvider = sessionInfoProvider;
            this.sessionManager = sessionManager;
            this.gameManager = gameManager;
            this.secureHasher = secureHasher;
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


            var session = GetSessionToken();
            AssertSessionTokenValidity(session);

            return gameManager.UseScroll(session, characterId, st);
        }

        //#region Admin Player Control
        //[HttpGet("{userId}/join/{targetUserId}")]
        //public bool Join(string userId, string targetUserId)
        //{
        //    AssertAdminAuthToken(GetAuthToken());
        //    return gameManager.Join(userId, targetUserId);
        //}

        //[HttpGet("{userId}/leave")]
        //public bool Leave(string userId)
        //{
        //    AssertAdminAuthToken(GetAuthToken());
        //    return gameManager.Leave(userId);
        //}

        //[HttpGet("{userId}/walkto/{x}/{y}/{z}")]
        //public bool WalkTo(string userId, int x, int y, int z)
        //{
        //    AssertAdminAuthToken(GetAuthToken());
        //    return gameManager.WalkTo(userId, x, y, z);
        //}

        //[HttpGet("{userId}/attack/{targetId}/{type}")]
        //public bool Attack(string userId, string targetId, int type)
        //{
        //    AssertAdminAuthToken(GetAuthToken());
        //    return gameManager.Attack(userId, targetId, (AttackType)type);
        //}

        //[HttpGet("{userId}/object-action/{targetId}/{type}")]
        //public bool ObjectAction(string userId, string targetId, int type)
        //{
        //    AssertAdminAuthToken(GetAuthToken());
        //    return gameManager.ObjectAction(userId, targetId, (ObjectActionType)type);
        //}

        //[HttpGet("{userId}/task/{task}/{taskArgument}")]
        //public bool SetTask(string userId, string task, string taskArgument)
        //{
        //    AssertAdminAuthToken(GetAuthToken());
        //    return gameManager.SetTask(userId, task, taskArgument);
        //}

        //[HttpGet("{userId}/task/{task}")]
        //public bool SetTask(string userId, string task)
        //{
        //    AssertAdminAuthToken(GetAuthToken());
        //    return gameManager.SetTask(userId, task, task);
        //}

        //[HttpGet("{userId}/raid")]
        //public bool JoinRaid(string userId)
        //{
        //    AssertAdminAuthToken(GetAuthToken());
        //    return gameManager.JoinRaid(userId);
        //}

        //[HttpGet("{userId}/dungeon")]
        //public bool JoinDungeon(string userId)
        //{
        //    AssertAdminAuthToken(GetAuthToken());
        //    return gameManager.JoinDungeon(userId);
        //}

        //[HttpGet("{userId}/arena")]
        //public bool JoinArena(string userId)
        //{
        //    AssertAdminAuthToken(GetAuthToken());
        //    return gameManager.JoinArena(userId);
        //}

        //[HttpGet("{userId}/duel/accept")]
        //public bool DuelAccept(string userId)
        //{
        //    AssertAdminAuthToken(GetAuthToken());
        //    return gameManager.DuelAccept(userId);
        //}

        //[HttpGet("{userId}/duel/decline")]
        //public bool DuelDecline(string userId)
        //{
        //    AssertAdminAuthToken(GetAuthToken());
        //    return gameManager.DuelDecline(userId);
        //}

        //[HttpGet("{userId}/duel/{targetUserId}")]
        //public bool DuelRequest(string userId, string targetUserId)
        //{
        //    AssertAdminAuthToken(GetAuthToken());
        //    return gameManager.DuelRequest(userId, targetUserId);
        //}

        //[HttpGet("{userId}/travel/{island}")]
        //public bool IslandTravel(string userId, string island)
        //{
        //    AssertAdminAuthToken(GetAuthToken());
        //    return gameManager.Travel(userId, island);
        //}

        //[HttpGet("{userId}/travel")]
        //public bool Travel(string userId)
        //{
        //    AssertAdminAuthToken(GetAuthToken());
        //    return gameManager.Travel(userId, null);
        //}

        //#endregion

        //#region Player Control

        //[HttpGet("join/{targetUserId}")]
        //public bool UserJoin(string targetUserId)
        //{
        //    return gameManager.Join(GetCurrentUser().UserId, targetUserId);
        //}

        //[HttpGet("leave")]
        //public bool UserLeave()
        //{
        //    return gameManager.Leave(GetCurrentUser().UserId);
        //}

        //[HttpGet("walkto/{x}/{y}/{z}")]
        //public bool UserWalkTo(int x, int y, int z)
        //{
        //    return gameManager.WalkTo(GetCurrentUser().UserId, x, y, z);
        //}

        //[HttpGet("attack/{targetId}/{type}")]
        //public bool UserAttack(string targetId, int type)
        //{
        //    return gameManager.Attack(GetCurrentUser().UserId, targetId, (AttackType)type);
        //}

        //[HttpGet("object-action/{targetId}/{type}")]
        //public bool UserObjectAction(string targetId, int type)
        //{
        //    return gameManager.ObjectAction(GetCurrentUser().UserId, targetId, (ObjectActionType)type);
        //}

        //[HttpGet("task/{task}/{taskArgument}")]
        //public bool UserSetTask(string task, string taskArgument)
        //{
        //    return gameManager.SetTask(GetCurrentUser().UserId, task, taskArgument);
        //}

        //[HttpGet("task/{task}")]
        //public bool UserSetTask(string task)
        //{
        //    return gameManager.SetTask(GetCurrentUser().UserId, task, task);
        //}

        //[HttpGet("raid")]
        //public bool UserJoinRaid()
        //{
        //    return gameManager.JoinRaid(GetCurrentUser().UserId);
        //}

        //[HttpGet("dungeon")]
        //public bool UserJoinDungeon()
        //{
        //    return gameManager.JoinDungeon(GetCurrentUser().UserId);
        //}

        //[HttpGet("arena")]
        //public bool UserJoinArena()
        //{
        //    return gameManager.JoinArena(GetCurrentUser().UserId);
        //}

        //[HttpGet("duel/accept")]
        //public bool UserDuelAccept()
        //{
        //    return gameManager.DuelAccept(GetCurrentUser().UserId);
        //}

        //[HttpGet("duel/decline")]
        //public bool UserDuelDecline()
        //{
        //    return gameManager.DuelDecline(GetCurrentUser().UserId);
        //}

        //[HttpGet("duel/{targetUserId}")]
        //public bool UserDuelRequest(string targetUserId)
        //{
        //    return gameManager.DuelRequest(GetCurrentUser().UserId, targetUserId);
        //}

        //[HttpGet("travel")]
        //public bool UserTravel()
        //{
        //    return gameManager.Travel(GetCurrentUser().UserId, null);
        //}

        //[HttpGet("travel/{island}")]
        //public bool UserIslandTravel(string island)
        //{
        //    return gameManager.Travel(GetCurrentUser().UserId, island);
        //}
        //#endregion
    }
}

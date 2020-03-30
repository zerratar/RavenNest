using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using RavenNest.BusinessLogic;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Game;
using RavenNest.Models;
using RavenNest.Sessions;

namespace RavenNest.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    //[ApiDescriptor(Name = "Game API", Description = "Used for handling game sessions and polling game events.")]
    public class GameController : ControllerBase
    {
        private readonly IGameData gameData;
        private readonly IAuthManager authManager;
        private readonly ISessionInfoProvider sessionInfoProvider;
        private readonly ISessionManager sessionManager;
        private readonly IGameManager gameManager;
        private readonly ISecureHasher secureHasher;

        public GameController(
            IGameData gameData,
            IAuthManager authManager,
            ISessionInfoProvider sessionInfoProvider,
            ISessionManager sessionManager,
            IGameManager gameManager,
            ISecureHasher secureHasher)
        {
            this.gameData = gameData;
            this.authManager = authManager;
            this.sessionInfoProvider = sessionInfoProvider;
            this.sessionManager = sessionManager;
            this.gameManager = gameManager;
            this.secureHasher = secureHasher;
        }


        //[HttpGet("youcrazyone")]
        //public bool DoCrazyStuff()
        //{
        //    var characters = gameData.GetCharacters(x => true);
        //    foreach (var c in characters)
        //    {
        //        //var items = gameData.GetInventoryItems(c.Id);
        //        //foreach (var eq in items)
        //        //{
        //        //    if (eq.Amount == 0)
        //        //    {
        //        //        gameData.Remove(eq);
        //        //    }
        //        //    //var item = gameData.GetItem(eq.ItemId);
        //        //    //if (item.Category == (int)DataModels.ItemCategory.Resource)
        //        //    //{
        //        //    //    var stack = gameData.GetInventoryItem(c.Id, eq.ItemId);
        //        //    //    if (stack != null)
        //        //    //    {
        //        //    //        stack.Amount += eq.Amount;
        //        //    //        gameData.Remove(eq);
        //        //    //    }
        //        //    //    else
        //        //    //    {
        //        //    //        eq.Equipped = false;
        //        //    //    }
        //        //    //}
        //        //}
        //        var items = gameData.GetInventoryItems(c.Id);
        //        foreach (var stack in items.GroupBy(x => x.ItemId))
        //        {
        //            gameData.RemoveRange(stack.ToList());
        //            var newAmount = stack.Sum(x => x.Amount);
        //            gameData.Add(new DataModels.InventoryItem
        //            {
        //                Amount = newAmount,
        //                CharacterId = c.Id,
        //                Id = Guid.NewGuid(),
        //                ItemId = stack.Key,
        //                Equipped = false
        //            });
        //        }
        //    }
        //    //}
        //    gameData.Flush();

        //    return true;
        //}

        [HttpGet]
        public GameInfo Get()
        {
            var session = GetSessionToken();
            AssertSessionTokenValidity(session);
            return gameManager.GetGameInfo(session);
        }

        [HttpGet("{userId}/join/{targetUserId}")]
        public bool Join(string userId, string targetUserId)
        {
            AssertAdminAuthToken(GetAuthToken());
            return gameManager.Join(userId, targetUserId);
        }

        [HttpGet("{userId}/leave")]
        public bool Leave(string userId)
        {
            AssertAdminAuthToken(GetAuthToken());
            return gameManager.Leave(userId);
        }

        [HttpGet("{userId}/walkto/{x}/{y}/{z}")]
        public bool WalkTo(string userId, int x, int y, int z)
        {
            AssertAdminAuthToken(GetAuthToken());
            return gameManager.WalkTo(userId, x, y, z);
        }

        [HttpGet("{userId}/attack/{targetId}/{type}")]
        public bool Attack(string userId, string targetId, int type)
        {
            AssertAdminAuthToken(GetAuthToken());
            return gameManager.Attack(userId, targetId, (AttackType)type);
        }

        [HttpGet("{userId}/object-action/{targetId}/{type}")]
        public bool ObjectAction(string userId, string targetId, int type)
        {
            AssertAdminAuthToken(GetAuthToken());
            return gameManager.ObjectAction(userId, targetId, (ObjectActionType)type);
        }

        [HttpGet("{userId}/task/{task}/{taskArgument}")]
        public bool SetTask(string userId, string task, string taskArgument)
        {
            AssertAdminAuthToken(GetAuthToken());
            return gameManager.SetTask(userId, task, taskArgument);
        }

        [HttpGet("{userId}/task/{task}")]
        public bool SetTask(string userId, string task)
        {
            AssertAdminAuthToken(GetAuthToken());
            return gameManager.SetTask(userId, task, task);
        }

        [HttpGet("{userId}/raid")]
        public bool JoinRaid(string userId)
        {
            AssertAdminAuthToken(GetAuthToken());
            return gameManager.JoinRaid(userId);
        }

        [HttpGet("{userId}/dungeon")]
        public bool JoinDungeon(string userId)
        {
            AssertAdminAuthToken(GetAuthToken());
            return gameManager.JoinDungeon(userId);
        }

        [HttpGet("{userId}/arena")]
        public bool JoinArena(string userId)
        {
            AssertAdminAuthToken(GetAuthToken());
            return gameManager.JoinArena(userId);
        }

        [HttpGet("{userId}/duel/accept")]
        public bool DuelAccept(string userId)
        {
            AssertAdminAuthToken(GetAuthToken());
            return gameManager.DuelAccept(userId);
        }

        [HttpGet("{userId}/duel/decline")]
        public bool DuelDecline(string userId)
        {
            AssertAdminAuthToken(GetAuthToken());
            return gameManager.DuelDecline(userId);
        }

        [HttpGet("{userId}/duel/{targetUserId}")]
        public bool DuelDecline(string userId, string targetUserId)
        {
            AssertAdminAuthToken(GetAuthToken());
            return gameManager.DuelRequest(userId, targetUserId);
        }

        [HttpGet("{userId}/travel")]
        public bool Travel(string userId, string island)
        {
            AssertAdminAuthToken(GetAuthToken());
            return gameManager.Travel(userId, island);
        }

        [HttpGet("{userId}/travel/{island}")]
        public bool Travel(string userId)
        {
            AssertAdminAuthToken(GetAuthToken());
            return gameManager.Travel(userId, null);
        }

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

            if (session == null)
            {
                HttpContext.Response.StatusCode = 403;
                return null;
            }

            return session;
        }

        [HttpDelete("raid/{username}")]
        public bool EndSessionAndRaid(string username, Single<bool> war)
        {
            var session = GetSessionToken();
            AssertSessionTokenValidity(session);
            return this.sessionManager.EndSessionAndRaid(session, username, war.Value);
        }

        [HttpDelete]
        public void EndSession()
        {
            var session = GetSessionToken();
            AssertSessionTokenValidity(session);
            this.sessionManager.EndSession(session);
        }

        private SessionToken GetSessionToken()
        {
            if (HttpContext.Request.Headers.TryGetValue("session-token", out var value))
            {
                return sessionManager.Get(value);
            }
            return null;
        }

        private AuthToken GetAuthToken()
        {
            if (HttpContext.Request.Headers.TryGetValue("auth-token", out var value))
                return authManager.Get(value);
            if (sessionInfoProvider.TryGetAuthToken(HttpContext.Session, out var authToken))
                return authToken;
            return null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AssertAdminAuthToken(AuthToken authToken)
        {
            var user = gameData.GetUser(authToken.UserId);
            if (!user.IsAdmin.GetValueOrDefault())
                throw new Exception("You do not have permissions to call this API");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AssertAuthTokenValidity(AuthToken authToken)
        {
            if (authToken == null) throw new NullReferenceException(nameof(authToken));
            if (authToken.UserId == Guid.Empty) throw new NullReferenceException(nameof(authToken.UserId));
            if (authToken.Expired) throw new Exception("Session has expired.");
            if (string.IsNullOrEmpty(authToken.Token)) throw new Exception("Session has expired.");
            if (authToken.Token != secureHasher.Get(authToken))
            {
                throw new Exception("Session has expired.");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void AssertSessionTokenValidity(SessionToken sessionToken)
        {
            if (sessionToken == null) throw new NullReferenceException(nameof(sessionToken));
            if (sessionToken.SessionId == Guid.Empty) throw new NullReferenceException(nameof(sessionToken.SessionId));
            if (sessionToken.Expired) throw new Exception("Session has expired.");
        }
    }

}
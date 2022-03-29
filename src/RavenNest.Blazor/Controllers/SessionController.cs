using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Game;
using RavenNest.Models;
using RavenNest.Sessions;

namespace RavenNest.Controllers
{
    [ApiExplorerSettings(IgnoreApi = true)]
    [Route("api/[controller]")]
    [ApiController]
    //[ApiDescriptor(Name = "Game API", Description = "Used for handling game sessions and polling game events.")]
    public class SessionController : GameApiController
    {
        private readonly IGameData gameData;
        private readonly IAuthManager authManager;
        private readonly ISessionManager sessionManager;
        private readonly IGameManager gameManager;
        private readonly ISecureHasher secureHasher;

        public SessionController(
            ILogger<SessionController> logger,
            IGameData gameData,
            ISessionInfoProvider sessionInfoProvider,
            IAuthManager authManager,
            ISessionManager sessionManager,
            IGameManager gameManager,
            ISecureHasher secureHasher)
            : base(logger, gameData, authManager, sessionInfoProvider, sessionManager, secureHasher)
        {
            this.gameData = gameData;
            this.authManager = authManager;
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

        [HttpDelete("raid/{username}")]
        public bool EndSessionAndRaid(string username, Single<bool> war)
        {
            var session = GetSessionToken();
            AssertSessionTokenValidity(session);
            return this.sessionManager.EndSessionAndRaid(session, username, war.Value);
        }


        [HttpPost("raid/{username}")]
        public bool PostEndSessionAndRaid(string username, Single<bool> war)
        {
            var session = GetSessionToken();
            AssertSessionTokenValidity(session);
            return this.sessionManager.EndSessionAndRaid(session, username, war.Value);
        }

        [HttpPost]
        public void PostEndSession()
        {
            var session = GetSessionToken();
            AssertSessionTokenValidity(session);
            this.sessionManager.EndSession(session);
        }

        [HttpDelete]
        public void EndSession()
        {
            var session = GetSessionToken();
            AssertSessionTokenValidity(session);
            this.sessionManager.EndSession(session);
        }
    }
}

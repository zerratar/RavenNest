using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Game;
using RavenNest.Sessions;
using Shinobytes.OpenAI.Models;
using System.Text.Json.Serialization;

namespace RavenNest.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OpenAIController : GameApiController
    {
        private readonly GameData gameData;
        private readonly IAuthManager authManager;
        private readonly SessionInfoProvider sessionInfoProvider;
        private readonly SessionManager sessionManager;
        private readonly GameManager gameManager;
        private readonly ClanManager clanManager;
        private readonly ISecureHasher secureHasher;
        private readonly ILogger<OpenAIController> logger;

        public OpenAIController(
            ILogger<OpenAIController> logger,
            GameData gameData,
            IAuthManager authManager,
            SessionInfoProvider sessionInfoProvider,
            SessionManager sessionManager,
            GameManager gameManager,
            ClanManager clanManager,
            ISecureHasher secureHasher)
            : base(logger, gameData, authManager, sessionInfoProvider, sessionManager, secureHasher)
        {
            this.logger = logger;
            this.gameData = gameData;
            this.authManager = authManager;
            this.sessionInfoProvider = sessionInfoProvider;
            this.sessionManager = sessionManager;
            this.gameManager = gameManager;
            this.clanManager = clanManager;
            this.secureHasher = secureHasher;
        }

        [HttpGet]
        public Message[] Get()
        {
            var serverTime = System.DateTime.UtcNow;
            var facts = new string[]
            {

                $"Current server time is: {serverTime}"
            };

            return new[]
            {
                Message.Create("system", string.Join("\n", facts))
            };
        }
    }
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Game;
using RavenNest.Sessions;
using System.Text.Json.Serialization;

namespace RavenNest.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OpenAIController : GameApiController
    {
        private readonly IGameData gameData;
        private readonly IAuthManager authManager;
        private readonly ISessionInfoProvider sessionInfoProvider;
        private readonly ISessionManager sessionManager;
        private readonly IGameManager gameManager;
        private readonly IClanManager clanManager;
        private readonly ISecureHasher secureHasher;
        private readonly ILogger<OpenAIController> logger;

        public OpenAIController(
            ILogger<OpenAIController> logger,
            IGameData gameData,
            IAuthManager authManager,
            ISessionInfoProvider sessionInfoProvider,
            ISessionManager sessionManager,
            IGameManager gameManager,
            IClanManager clanManager,
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
        public ChatMessage[] Get()
        {
            var serverTime = System.DateTime.UtcNow;
            var facts = new string[]
            {

                $"Current server time is: {serverTime}"
            };

            return new[]
            {
                ChatMessage.Create("system", string.Join("\n", facts))
            };
        }



        public class ChatMessage
        {
            [JsonPropertyName("role")]
            public string? Role { get; set; }
            [JsonPropertyName("content")]
            public string? Content { get; set; }

            internal static ChatMessage Create(string role, string prompt)
            {
                return new ChatMessage
                {
                    Role = role,
                    Content = prompt,
                };
            }
        }

    }
}

using System;
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
    /// <summary>The Mangrove island (docs/banana-island.md in the game): treasure, the pirates' chest, the regatta, feeding the monkeys.</summary>
    [Route("api/[controller]")]
    [ApiController]
    public class MangroveController : GameApiController
    {
        private readonly MangroveManager mangroveManager;

        public MangroveController(
            ILogger<MangroveController> logger,
            GameData gameData,
            IAuthManager authManager,
            SessionInfoProvider sessionInfoProvider,
            SessionManager sessionManager,
            MangroveManager mangroveManager,
            ISecureHasher secureHasher)
            : base(logger, gameData, authManager, sessionInfoProvider, sessionManager, secureHasher)
        {
            this.mangroveManager = mangroveManager;
        }

        /// <summary>A chest for everyone who dug up a pirate's treasure ("treasure") or held the pirates' raid ("pirates").</summary>
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("{kind}/chest")]
        public MangroveReward[] Chest(string kind, Many<Guid> characterIds)
        {
            var sessionToken = GetSessionToken();
            AssertSessionTokenValidity(sessionToken);
            return mangroveManager.OpenChest(sessionToken, kind, characterIds?.Values);
        }

        /// <summary>The regatta's finishers, first to last.</summary>
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("regatta")]
        public MangroveReward[] Regatta(Many<Guid> characterIds)
        {
            var sessionToken = GetSessionToken();
            AssertSessionTokenValidity(sessionToken);
            return mangroveManager.RegattaPrizes(sessionToken, characterIds?.Values);
        }

        /// <summary>!banana feed: up to this many of the character's bananas ("banana") or Golden Bananas ("golden") go to the monkeys.</summary>
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("{characterId}/feed/{kind}/{amount}")]
        public long Feed(Guid characterId, string kind, long amount)
        {
            var sessionToken = GetSessionToken();
            AssertSessionTokenValidity(sessionToken);
            return mangroveManager.Feed(sessionToken, characterId, kind, amount);
        }

        /// <summary>Coins a character pays on the island (meddling in the regatta): the coins left, or -1 when they had too few.</summary>
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("{characterId}/spend/{coins}")]
        public long Spend(Guid characterId, long coins)
        {
            var sessionToken = GetSessionToken();
            AssertSessionTokenValidity(sessionToken);
            return mangroveManager.Spend(sessionToken, characterId, coins);
        }
    }
}

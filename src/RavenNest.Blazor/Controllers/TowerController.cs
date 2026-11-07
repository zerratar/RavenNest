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
    /// <summary>
    ///     The tower for the game (the game's docs/tower-design.md): the stream's floor and quests, kills on the floor,
    ///     boss attempts and clears. Always the calling game session's stream.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class TowerController : GameApiController
    {
        private readonly TowerManager tower;

        public TowerController(
            ILogger<TowerController> logger,
            GameData gameData,
            IAuthManager authManager,
            SessionInfoProvider sessionInfoProvider,
            SessionManager sessionManager,
            TowerManager tower,
            ISecureHasher secureHasher)
            : base(logger, gameData, authManager, sessionInfoProvider, sessionManager, secureHasher)
        {
            this.tower = tower;
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet]
        public TowerState Get()
        {
            var token = GetSessionToken();
            AssertSessionTokenValidity(token);
            return tower.GetState(token);
        }

        /// <summary>Kills on the floor since the last report: "defeat-hall-goblin:3;defeat-hall-bunny:5".</summary>
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("{floor}/progress/{counts}")]
        public TowerState Progress(int floor, string counts)
        {
            var token = GetSessionToken();
            AssertSessionTokenValidity(token);
            return tower.AddProgress(token, floor, Uri.UnescapeDataString(counts ?? ""));
        }

        /// <summary>Kills per character and kind on the tower's floors, for the characters' own quests.</summary>
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("kills")]
        public bool Kills([FromBody] TowerKillReport report)
        {
            var token = GetSessionToken();
            AssertSessionTokenValidity(token);
            tower.AddCharacterKills(token, report);
            return true;
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("{floor}/boss")]
        public TowerState BossStarted(int floor)
        {
            var token = GetSessionToken();
            AssertSessionTokenValidity(token);
            return tower.BossStarted(token, floor);
        }

        /// <summary>The boss fell: everyone who took part.</summary>
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("{floor}/cleared")]
        public TowerClearResult Cleared(int floor, Many<Guid> characterIds)
        {
            var token = GetSessionToken();
            AssertSessionTokenValidity(token);
            return tower.BossWon(token, floor, characterIds?.Values);
        }
    }
}

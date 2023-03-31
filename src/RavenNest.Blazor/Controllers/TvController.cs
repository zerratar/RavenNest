using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Game;
using RavenNest.Models.Tv;

namespace RavenNest.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TvController : GameApiController
    {
        public TvController(
            ILogger<TvController> logger,
            GameData gameData,
            IAuthManager authManager,
            SessionInfoProvider sessionInfoProvider,
            SessionManager sessionManager,
            ISecureHasher secureHasher)
            : base(logger, gameData, authManager, sessionInfoProvider, sessionManager, secureHasher)
        {
        }

        [HttpGet("episode/{episodeId}")]
        public async Task<EpisodeResult> GetEpisodeAsync(Guid episodeId)
        {
            // not implemented yet

            return new EpisodeResult()
            {
                Id = episodeId,
                Status = EpisodeGenerationStatus.NotFound,
            };
        }

        [HttpPost("episode")]
        public async Task<EpisodeResult> GenerateEpisodeAsync([FromBody] GenerateEpisodeRequest request)
        {
            // not implemented yet

            return new EpisodeResult()
            {
                Id = request.Id,
                Status = EpisodeGenerationStatus.Error,
            };
        }
    }
}

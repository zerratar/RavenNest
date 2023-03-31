using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Game;
using RavenNest.Models.Tv;

namespace RavenNest.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TvController : GameApiController
    {
        private readonly RavenfallTvManager tv;
        public TvController(
            ILogger<TvController> logger,
            GameData gameData,
            IAuthManager authManager,
            RavenfallTvManager tv,
            SessionInfoProvider sessionInfoProvider,
            SessionManager sessionManager,
            ISecureHasher secureHasher)
            : base(logger, gameData, authManager, sessionInfoProvider, sessionManager, secureHasher)
        {
            this.tv = tv;
        }

        [HttpGet("episode/{episodeId}")]
        public async Task<ActionResult> GetEpisodeAsync(Guid episodeId)
        {
            var authToken = GetAuthToken();
            if (!IsAuthTokenValid(authToken, out var err))
            {
                return Unauthorized(err);
            }

            var result = await tv.GetEpisodeAsync(episodeId);

            return new JsonResult(result);
        }

        [HttpPost("episode")]
        public async Task<ActionResult> GenerateEpisodeAsync([FromBody] GenerateEpisodeRequest request)
        {
            var authToken = GetAuthToken();
            if (!IsAuthTokenValid(authToken, out var err))
            {
                return Unauthorized(err);
            }

            // check if user is a patron, if not. then we have to return an unauthorized.
            var user = GameData.GetUser(authToken.UserId);
            if (user.PatreonTier <= 0)
            {
                return Unauthorized("You must be a patron to generate episodes.");
            }

            var result = await tv.GenerateEpisodeAsync(user, request);
            return new JsonResult(result);
        }
    }
}

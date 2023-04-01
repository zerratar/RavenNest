using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Game;
using RavenNest.BusinessLogic.Tv;
using RavenNest.Models.Tv;

namespace RavenNest.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TvController : GameApiController
    {
        private const int MinEpisodePerRequest = 1;
        private const int MaxEpisodePerRequest = 10;
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

        [HttpGet("episodes/{date}/{take}")]
        public async Task<ActionResult> GetEpisodesAsync(DateTime date, int take)
        {
            var authToken = GetAuthToken();
            if (!IsAuthTokenValid(authToken, out var err))
            {
                return Unauthorized(err);
            }

            // // No need to check for patreon, although it could be one way to check whether or not to check for personal shows
            // // For now, we will just check 
            //var user = GameData.GetUser(authToken.UserId);
            //if (user.PatreonTier <= 0)
            //{
            //    return Unauthorized("You must be a patron to generate episodes.");
            //}

            if (date >= DateTime.UtcNow)
            {
                // Date cannot be in the future, we will just return 0 entries.
                return new JsonResult(Array.Empty<Episode>());
            }

            // needs to be newer than the date provided. and no more than the take value.
            // since we should not download more than necessary
            take = Math.Clamp(take, MinEpisodePerRequest, MaxEpisodePerRequest);
            var episodes = await tv.GetEpisodesAsync(authToken.UserId, date, take);
            return new JsonResult(episodes);
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

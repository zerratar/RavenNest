using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Game;
using System;

namespace RavenNest.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ROBotController : ControllerBase
    {
        private readonly ILogger<ROBotController> logger;
        private readonly IServerManager serverManager;

        public ROBotController(
            ILogger<ROBotController> logger,
            IServerManager serverManager)
        {
            this.logger = logger;
            this.serverManager = serverManager;
        }

        [HttpPost("stats")]
        public void OnStatsPosted([FromBody] StreamBotStats stats)
        {
            serverManager.UpdateBotStats(stats);
        }
    }
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Game;
using System;
using System.IO;
using System.Threading.Tasks;

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
        public async Task OnStatsPosted()
        {
            var contentLength = HttpContext.Request.ContentLength;
            if (contentLength > 0)
            {
                var sr = new StreamReader(HttpContext.Request.Body);
                var data = await sr.ReadToEndAsync();
                serverManager.UpdateBotStats(data);
            }
        }
    }
}

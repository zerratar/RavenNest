using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using RavenNest.BusinessLogic;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Game;
using RavenNest.BusinessLogic.Patreon;
using RavenNest.BusinessLogic.Providers;
using RavenNest.Sessions;
using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace RavenNest.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [ApiExplorerSettings(IgnoreApi = true)]
    public class PatreonController : ControllerBase
    {
        private readonly ILogger<PatreonController> logger;
        private readonly ISessionInfoProvider sessionInfoProvider;
        private readonly ISessionManager sessionManager;
        private readonly IPlayerManager playerManager;
        private readonly IRavenfallDbContextProvider dbProvider;
        private readonly ISecureHasher secureHasher;
        private readonly IAuthManager authManager;
        private readonly IPatreonManager patreonManager;
        private readonly AppSettings settings;

        public PatreonController(
            ILogger<PatreonController> logger,
            ISessionInfoProvider sessionInfoProvider,
            IPlayerInventoryProvider inventoryProvider,
            ISessionManager sessionManager,
            IPlayerManager playerManager,
            IRavenfallDbContextProvider dbProvider,
            ISecureHasher secureHasher,
            IAuthManager authManager,
            IPatreonManager patreonManager,
            IOptions<AppSettings> settings)
        {
            this.logger = logger;
            this.sessionInfoProvider = sessionInfoProvider;
            this.sessionManager = sessionManager;
            this.playerManager = playerManager;
            this.dbProvider = dbProvider;
            this.secureHasher = secureHasher;
            this.authManager = authManager;
            this.patreonManager = patreonManager;
            this.settings = settings.Value;
        }

        [HttpPost("create")]
        public async Task Add()
        {
            //var sign = HttpContext.Request.Headers["x-signature"];
            //if (settings.PatreonCreatePledge != sign)
            //    return;

            var data = await GetPatreonDataAsync();
            if (data == null)
                return;

            patreonManager.AddPledge(data);
        }

        [HttpPost("update")]
        public async Task Update()
        {
            //var sign = HttpContext.Request.Headers["x-signature"];
            //if (settings.PatreonUpdatePledge != sign)
            //    return;

            var data = await GetPatreonDataAsync();
            if (data == null)
                return;

            patreonManager.UpdatePledge(data);
        }

        [HttpPost("delete")]
        public async Task Remove()
        {
            //var sign = HttpContext.Request.Headers["x-signature"];
            //if (settings.PatreonDeletePledge != sign)
            //    return;

            var data = await GetPatreonDataAsync();
            if (data == null)
                return;

            patreonManager.RemovePledge(data);
        }


        private async Task<IPatreonData> GetPatreonDataAsync()
        {
            var data = await GetRequestData<ZapierPatreonData>();
            if (data == null)
                return null;

            string title = data.RewardTitle;
            if (!string.IsNullOrEmpty(title) && title.Contains(','))
            {
                title = title.Split(',')[1];
                data.RewardTitle = title;
            }

            data.Tier = GetRewardTier(title);
            return data;
        }

        private int GetRewardTier(string rewardTitle)
        {
            if (string.IsNullOrEmpty(rewardTitle))
                return 0;

            var title = rewardTitle.ToLower();
            switch (title)
            {
                default:
                    return 0;
                case "mithril":
                    return 1;
                case "adamantite":
                case "rune":
                    return 2;
                case "dragon":
                    return 3;
                case "abraxas":
                    return 4;
                case "phantom":
                    return 5;
            }
        }

        private async Task<T> GetRequestData<T>([CallerMemberName] string caller = null)
        {
            string patreonJson = "";
            try
            {
                using (var sr = new StreamReader(HttpContext.Request.Body))
                {
                    patreonJson = await sr.ReadToEndAsync();
                    logger.LogError("[" + caller + "] Patreon Data Received: " + patreonJson);
                    var patreonDataFolder = new DirectoryInfo("patreon-data");
                    if (!System.IO.Directory.Exists("patreon-data"))
                    {
                        patreonDataFolder = Directory.CreateDirectory("patreon-data");
                    }

                    System.IO.File.WriteAllText(Path.Combine(patreonDataFolder.FullName,
                        caller + "_" + DateTime.UtcNow.ToString("yyyy-MM-dd.hhmmss") + ".json"), patreonJson);

                    return JsonConvert.DeserializeObject<T>(patreonJson);
                }
            }
            catch (Exception exc)
            {
                logger.LogError(exc.ToString() + "\r\n\r\n" + patreonJson);
                return default;
            }
        }
    }
}

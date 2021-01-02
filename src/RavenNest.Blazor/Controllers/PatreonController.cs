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
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace RavenNest.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
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

        [HttpPost("update-pledge")]
        public async Task UpdatePledge()
        {
            AssertValidSignature(settings.PatreonUpdatePledge);
            var data = ConvertPledgeData(await GetPatreonData());
            if (data == null) return;
            patreonManager.UpdatePledge(data);
        }

        [HttpPost("delete-pledge")]
        public async Task DeletePledge()
        {
            AssertValidSignature(settings.PatreonDeletePledge);
            var data = ConvertPledgeData(await GetPatreonData());
            if (data == null) return;
            patreonManager.RemovePledge(data);
        }

        [HttpPost("create-pledge")]
        public async Task CreatePledge()
        {
            AssertValidSignature(settings.PatreonCreatePledge);
            var data = ConvertPledgeData(await GetPatreonData());
            if (data == null) return;
            patreonManager.AddPledge(data);
        }

        private void AssertValidSignature(string secret)
        {
            // X-Patreon-Event : to get the trigger, example: pledges:create, 
            // not used here, as we have separate endpoints for each.
            var signature = HttpContext.Request.Headers["X-Patreon-Signature"];
            // signature is the HEX digest of the message body HMAC signed (with MD5) using your webhook's secret
            // uh..
        }

        private PatreonPledgeData ConvertPledgeData(PatreonWebhook data)
        {
            if (data == null)
                return null;

            var fullName = data.Data.Attributes.FullName;
            var pledgeAmountCents = data.Data.Attributes.PledgeAmountCents;
            var status = data.Data.Attributes.PatronStatus;

            var userData = data.Included.FirstOrDefault(x => x.Type == TypeEnum.User);

            var reward = data.Included
                .Where(x => x.Type == TypeEnum.Reward)
                .OrderByDescending(x => x.Attributes.Amount)
                .FirstOrDefault();

            var isActive = status == "active_patreon" && reward != null;
            var social = userData.Attributes.SocialConnections;
            var twitchUserId = "";
            var rewardTitle = reward?.Attributes?.Title;
            int rewardTier = GetRewardTier(rewardTitle);
            if (social != null && social.Twitch != null)
            {
                twitchUserId = social.Twitch.UserId;
            }

            return new PatreonPledgeData
            {
                PatreonId = userData.Id,
                Email = userData.Attributes.Email,
                FirstName = userData.Attributes.FirstName,
                FullName = userData.Attributes.FullName,
                TwitchUserId = twitchUserId,
                PledgeAmountCents = pledgeAmountCents,
                RewardTitle = rewardTitle,
                Tier = rewardTier,
            };
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
                case "adamantite":
                    return 2;
                case "rune":
                    return 2;
                case "dragon":
                case "abraxas":
                case "phantom":
                    return 3;
            }
        }

        private async Task<PatreonWebhook> GetPatreonData([CallerMemberName] string caller = null)
        {
            try
            {
                using (var sr = new StreamReader(HttpContext.Request.Body))
                {
                    var data = await sr.ReadToEndAsync();
                    return JsonConvert.DeserializeObject<PatreonWebhook>(data);
                }
            }
            catch (Exception exc)
            {
                logger.LogError(exc.ToString());
                return null;
            }
        }
    }
}

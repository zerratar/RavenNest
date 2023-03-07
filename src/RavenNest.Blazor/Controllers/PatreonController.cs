using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Game;
using RavenNest.BusinessLogic.Models.Patreon.API;
using RavenNest.Sessions;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace RavenNest.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PatreonController : GameApiController
    {
        private readonly GameData gameData;
        private readonly IAuthManager authManager;
        private readonly IPatreonManager patreonManager;
        private readonly ISessionInfoProvider sessionInfoProvider;
        private readonly SessionManager sessionManager;
        private readonly GameManager gameManager;
        private readonly ClanManager clanManager;
        private readonly ISecureHasher secureHasher;
        private readonly ILogger<GameController> logger;

        public PatreonController(
            ILogger<GameController> logger,
            GameData gameData,
            IAuthManager authManager,
            IPatreonManager patreonManager,
            ISessionInfoProvider sessionInfoProvider,
            SessionManager sessionManager,
            GameManager gameManager,
            ClanManager clanManager,
            ISecureHasher secureHasher)
            : base(logger, gameData, authManager, sessionInfoProvider, sessionManager, secureHasher)
        {
            this.logger = logger;
            this.gameData = gameData;
            this.authManager = authManager;
            this.patreonManager = patreonManager;
            this.sessionInfoProvider = sessionInfoProvider;
            this.sessionManager = sessionManager;
            this.gameManager = gameManager;
            this.clanManager = clanManager;
            this.secureHasher = secureHasher;
        }

        [HttpGet]
        public string Get()
        {
            return "this is api, yes.";
        }

        [HttpPost]
        public async Task<string> OnWebHookReceived()
        {
            using var reader = new StreamReader(Request.Body);
            var content = await reader.ReadToEndAsync();
            var patreonEvent = HttpContext.Request.Headers["x-patreon-event"];
            var signature = HttpContext.Request.Headers["x-patreon-signature"];

            //AssertValidateSignature(signature);

            var data = ParseRequest(content);

            switch (patreonEvent)
            {
                case "members:pledge:delete":
                case "members:delete":
                    Delete(data);
                    break;

                case "members:pledge:update":
                case "members:update":
                case "members:pledge:create":
                case "members:create":
                    await CreateOrUpdateAsync(data);
                    break;
            }

            return "OK";
        }

        private async Task CreateOrUpdateAsync(PatreonInfo data)
        {
            var patreon = gameData.GetPatreonUser(data.PatreonId);
            if (patreon == null)
            {
                patreon = new DataModels.UserPatreon();
                patreon.Id = System.Guid.NewGuid();
                patreon.Created = System.DateTime.UtcNow;
                patreon.Updated = patreon.Created;
                gameData.Add(patreon);
            }
            else
            {
                patreon.Updated = System.DateTime.UtcNow;
            }

            patreon.Email = data.Email;
            patreon.PatreonId = data.PatreonId;
            // update and make sure tiers are all set.
            var isActive = data.Status == "active_patron";
            if (isActive)
            {
                var tier = await patreonManager.GetTierByCentsAsync(data.PledgeAmountCents);
                if (tier != null)
                {
                    patreon.Tier = tier.Level;
                    patreon.PledgeAmount = tier.AmountCents;
                    patreon.PledgeTitle = tier.Title;
                }
                else
                {
                    logger.LogError("Unable to find a patreon Tier with pledge amount: " + data.PledgeAmountCents + ".");
                }
            }
            else
            {
                patreon.Tier = null;
                patreon.PledgeTitle = null;
                patreon.PledgeAmount = null;
            }

            UpdateUser(data, patreon);
        }

        private void UpdateUser(PatreonInfo data, DataModels.UserPatreon patreon)
        {
            DataModels.User user = null;
            if (patreon.UserId != null)
            {
                user = gameData.GetUser(patreon.UserId.Value);
            }

            if (!string.IsNullOrEmpty(data.TwitchUserId))
            {
                patreon.TwitchUserId = data.TwitchUserId;

                if (user == null)
                {
                    user = gameData.GetUserByTwitchId(data.TwitchUserId);
                }
            }

            if (!string.IsNullOrEmpty(data.TwitchUsername))
            {
                if (user == null)
                {
                    user = gameData.GetUserByUsername(data.TwitchUsername);
                }
            }

            if (user != null)
            {
                user.PatreonTier = patreon.Tier;
            }
        }

        private void Delete(PatreonInfo data)
        {
            var patreon = gameData.GetPatreonUser(data.PatreonId);
            if (patreon == null) return;
            if (patreon.UserId != null)
            {
                var user = gameData.GetUser(patreon.UserId.Value);
                if (user != null)
                {
                    user.PatreonTier = null;
                }
            }

            patreon.PledgeTitle = null;
            patreon.PledgeAmount = null;
            patreon.Tier = null;
        }

        private static PatreonInfo ParseRequest(string content)
        {
            var json = JObject.Parse(content);
            var info = new PatreonInfo();
            var included = json["included"];

            foreach (var data in included.Children())
            {
                if ((string)data["type"] != "user")
                {
                    continue;
                }

                var attributes = data["attributes"];
                info.PatreonId = (long)data["id"];
                info.FullName = (string)attributes["full_name"];
                info.Email = (string)attributes["email"];

                var socialConnections = attributes["social_connections"];
                var twitch = socialConnections["twitch"];
                if (twitch != null)
                {
                    info.TwitchUserId = (string)twitch["user_id"];
                    info.TwitchUsername = ((string)twitch["url"])?.Split('/').LastOrDefault();
                }

                break;
            }

            info.Status = (string)json["data"]["attributes"]["patron_status"];
            info.PledgeAmountCents = (decimal)json["data"]["attributes"]["pledge_amount_cents"];
            info.PledgeAmountDollars = info.PledgeAmountCents / 100; // Convert cents to dollars
            return info;
        }

        private class PatreonInfo
        {
            public long PatreonId { get; set; }
            public string FullName { get; set; }
            public string Email { get; set; }
            public string Status { get; set; }
            public string TwitchUserId { get; set; }
            public string TwitchUsername { get; set; }
            public decimal PledgeAmountCents { get; set; }
            public decimal PledgeAmountDollars { get; set; }
        }
    }
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using RavenNest.BusinessLogic;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Game;
using RavenNest.Sessions;
using System;
using System.IO;
using System.Runtime.CompilerServices;

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
            IOptions<AppSettings> settings)
        {
            this.logger = logger;
            this.sessionInfoProvider = sessionInfoProvider;
            this.sessionManager = sessionManager;
            this.playerManager = playerManager;
            this.dbProvider = dbProvider;
            this.secureHasher = secureHasher;
            this.authManager = authManager;
            this.settings = settings.Value;
        }

        [HttpGet("update-meber")]
        public void UpdateMember()
        {
            AssertValidSignature(settings.PatreonUpdateMember);
            var data = GetPatreonData();
        }

        [HttpGet("create-meber")]
        public void CreateMember()
        {
            AssertValidSignature(settings.PatreonCreateMember);
            var data = GetPatreonData();
        }

        [HttpGet("delete-member")]
        public void DeleteMember()
        {
            AssertValidSignature(settings.PatreonDeleteMember);
            var data = GetPatreonData();
        }

        [HttpGet("update-pledge")]
        public void UpdatePledge()
        {
            AssertValidSignature(settings.PatreonUpdatePledge);
            var data = GetPatreonData();
        }

        [HttpGet("delete-pledge")]
        public void DeletePledge()
        {
            AssertValidSignature(settings.PatreonDeletePledge);
            var data = GetPatreonData();
        }


        [HttpGet("create-pledge")]
        public void CreatePledge()
        {
            AssertValidSignature(settings.PatreonCreatePledge);
            var data = GetPatreonData();
        }


        private void AssertValidSignature(string secret)
        {
            // X-Patreon-Event : to get the trigger, example: pledges:create, 
            // not used here, as we have separate endpoints for each.
            var signature = HttpContext.Request.Headers["X-Patreon-Signature"];
            // signature is the HEX digest of the message body HMAC signed (with MD5) using your webhook's secret
            // uh..
        }

        private PatreonWebhook GetPatreonData([CallerMemberName] string caller = null)
        {
            if (HttpContext.Request.Body == null)
                return null;

            try
            {
                using (var sr = new StreamReader(HttpContext.Request.Body))
                {
                    var data = sr.ReadToEnd();

                    try
                    {
                        var filename = caller ?? "patreon";

                        System.IO.File.WriteAllText(filename + ".json", data);
                    }
                    catch (Exception exc)
                    {
                        logger.LogError(exc.ToString());
                    }

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

    public partial class PatreonWebhook
    {
        public PatreonWebhookData Data { get; set; }
    }

    public partial class PatreonWebhookData
    {
        public Attributes Attributes { get; set; }
        public long Id { get; set; }
        public Relationships Relationships { get; set; }
        public string Type { get; set; }
    }

    public partial class Attributes
    {
        public long AmountCents { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public string DeclinedSince { get; set; }
        public bool PatronPaysFees { get; set; }
        public string PledgeCapCents { get; set; }
    }

    public partial class Relationships
    {
        public Address Address { get; set; }
        public Address Card { get; set; }
        public Creator Creator { get; set; }
        public Creator Patron { get; set; }
        public Creator Reward { get; set; }
    }

    public partial class Address
    {
        public string Data { get; set; }
    }

    public partial class Creator
    {
        public CreatorData Data { get; set; }
        public Links Links { get; set; }
    }

    public partial class CreatorData
    {
        public long Id { get; set; }
        public string Type { get; set; }
    }

    public partial class Links
    {
        public Uri Related { get; set; }
    }
}

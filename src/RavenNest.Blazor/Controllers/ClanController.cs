using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RavenNest.Blazor.Services;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Game;
using RavenNest.Models;
using RavenNest.Sessions;
using System;
using System.IO;
using System.Threading.Tasks;

namespace RavenNest.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ClanController : GameApiController
    {
        private readonly GameData gameData;
        private readonly IAuthManager authManager;
        private readonly SessionInfoProvider sessionInfoProvider;
        private readonly SessionManager sessionManager;
        private readonly GameManager gameManager;
        private readonly IClanManager clanManager;
        private readonly LogoService logoService;
        private readonly ISecureHasher secureHasher;
        private readonly ILogger<ClanController> logger;

        private readonly byte[] unknownClanLogoBytes;
        private readonly string unknownClanLogoUrl;

        public ClanController(
            ILogger<ClanController> logger,
            GameData gameData,
            IAuthManager authManager,
            SessionInfoProvider sessionInfoProvider,
            SessionManager sessionManager,
            GameManager gameManager,
            IClanManager clanManager,
            LogoService logoService,
            ISecureHasher secureHasher)
            : base(logger, gameData, authManager, sessionInfoProvider, sessionManager, secureHasher)
        {
            this.logoService = logoService;
            this.logger = logger;
            this.gameData = gameData;
            this.authManager = authManager;
            this.sessionInfoProvider = sessionInfoProvider;
            this.sessionManager = sessionManager;
            this.gameManager = gameManager;
            this.clanManager = clanManager;
            this.secureHasher = secureHasher;

            var b = unknownClanLogoUrl = "imgs/logo-tiny-black.png";

            if (!System.IO.File.Exists(b))
            {
                b = Path.Combine("wwwroot", b);
            }

            if (System.IO.File.Exists(b))
            {
                this.unknownClanLogoBytes = System.IO.File.ReadAllBytes(b);
            }
        }


        [HttpGet("logo/{userId}")]
        [ResponseCache(VaryByHeader = "User-Agent", Duration = 600)]
        public async Task<ActionResult> GetClanLogoAsync(string userId)
        {
            try
            {
                var imageData = await logoService.GetClanLogoAsync(userId);
                if (imageData != null)
                {
                    return File(imageData, "image/png");
                }

                if (unknownClanLogoBytes == null)
                {
                    return NotFound();
                }

                //return Redirect(unknownClanLogoUrl);
                return File(unknownClanLogoBytes, "image/png");
            }
            catch { }
            return NotFound();
        }


        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("stats/{characterId}")]
        public ClanStats GetClanStats(Guid characterId)
        {
            var session = GetSessionToken();
            AssertSessionTokenValidity(session);

            return clanManager.GetClanStats(characterId);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("info/{characterId}")]
        public ClanInfo GetClanInfo(Guid characterId)
        {
            var session = GetSessionToken();
            AssertSessionTokenValidity(session);

            return clanManager.GetClanInfo(characterId);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("invite/{senderCharacterId}/{characterId}")]
        public ClanInviteResult ClanInvitePlayer(Guid senderCharacterId, Guid characterId)
        {
            var session = GetSessionToken();
            AssertSessionTokenValidity(session);

            return clanManager.SendPlayerInvite(senderCharacterId, characterId);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("accept-invite/{characterId}/{argument}")]
        public JoinClanResult ClanInvitePlayerAccept(Guid characterId, string argument)
        {
            var session = GetSessionToken();
            AssertSessionTokenValidity(session);
            return clanManager.AcceptClanInvite(characterId, argument);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("decline-invite/{characterId}/{argument}")]
        public ClanDeclineResult ClanInvitePlayerDecline(Guid characterId, string argument)
        {
            var session = GetSessionToken();
            AssertSessionTokenValidity(session);
            return clanManager.DeclineClanInvite(characterId, argument);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("promote-member/{senderCharacterId}/{characterId}/{argument}")]
        public ChangeRoleResult ClanPromoteMember(Guid senderCharacterId, Guid characterId, string argument)
        {
            var session = GetSessionToken();
            AssertSessionTokenValidity(session);
            return clanManager.PromoteClanMember(senderCharacterId, characterId, argument);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("demote-member/{senderCharacterId}/{characterId}/{argument}")]
        public ChangeRoleResult ClanDemoteMember(Guid senderCharacterId, Guid characterId, string argument)
        {
            var session = GetSessionToken();
            AssertSessionTokenValidity(session);
            return clanManager.DemoteClanMember(senderCharacterId, characterId, argument);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("join/{clanOwnerId}/{characterId}")]
        public JoinClanResult ClanPlayerJoin(string clanOwnerId, Guid characterId)
        {
            var session = GetSessionToken();
            AssertSessionTokenValidity(session);
            return clanManager.JoinClan(clanOwnerId, "twitch", characterId);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("v2/join/{clanOwnerUserId}/{characterId}")]
        public JoinClanResult ClanPlayerJoin(Guid clanOwnerUserId, Guid characterId)
        {
            var session = GetSessionToken();
            AssertSessionTokenValidity(session);
            return clanManager.JoinClan(clanOwnerUserId, characterId);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("join/{platform}/{ownerPlatformId}/{characterId}")]
        public JoinClanResult ClanPlayerJoin(string clanOwnerId, string platform, Guid characterId)
        {
            var session = GetSessionToken();
            AssertSessionTokenValidity(session);
            return clanManager.JoinClan(clanOwnerId, platform, characterId);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("leave/{characterId}")]
        public ClanLeaveResult ClanPlayerLeave(Guid characterId)
        {
            var session = GetSessionToken();
            AssertSessionTokenValidity(session);
            return clanManager.LeaveClan(characterId);
        }


        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("remove/{senderCharacterId}/{characterId}")]
        public bool ClanPlayerRemove(Guid senderCharacterId, Guid characterId)
        {
            var session = GetSessionToken();
            AssertSessionTokenValidity(session);
            return clanManager.RemoveClanMember(characterId, senderCharacterId);
        }
    }
}

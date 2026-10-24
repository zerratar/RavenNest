using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RavenNest.Blazor.Services;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Game;
using RavenNest.BusinessLogic.Game.ClanImages;
using RavenNest.BusinessLogic.Extended;
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
        private readonly ClanImageManager clanImages;
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
            ClanImageManager clanImages,
            ISecureHasher secureHasher)
            : base(logger, gameData, authManager, sessionInfoProvider, sessionManager, secureHasher)
        {
            this.logoService = logoService;
            this.clanImages = clanImages;
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
                // the clan's own logo when the owner uploaded one, otherwise their Twitch picture
                var imageData = clanImages.GetLiveLogoByOwnerTwitchId(userId) ?? await logoService.GetClanLogoAsync(userId);
                if (imageData != null)
                {
                    return File(imageData, "image/png");
                }

                if (unknownClanLogoBytes == null)
                {
                    return NotFound();
                }

                //return Redirect(unknownClanLogoUrl);
                // the game tries a placeholder again later, as the real logo may be there by then
                Response.Headers["X-Logo-Fallback"] = "true";
                return File(unknownClanLogoBytes, "image/png");
            }
            catch { }
            return NotFound();
        }

        /// <summary>A clan's logo and cape images and cape shapes, for the game.</summary>
        [HttpGet("look/{clanId}")]
        public ActionResult<ClanLook> GetClanLook(Guid clanId)
        {
            var look = clanImages.GetLook(clanId);
            return look != null ? look : NotFound();
        }

        /// <summary>
        ///     A clan image by id. Live images are for everyone and never change (a new image has a new id);
        ///     others only for the staff and the clan's owner, for the admin page and the clan page.
        /// </summary>
        [HttpGet("image/{imageId}")]
        public ActionResult GetClanImage(string imageId)
        {
            if (!Guid.TryParse(Path.GetFileNameWithoutExtension(imageId), out var id)) return NotFound();
            var data = clanImages.GetImage(id, WebsiteUser());
            if (data == null) return NotFound();

            Response.Headers["Cache-Control"] = clanImages.IsLive(id) ? "public, max-age=31536000, immutable" : "private, no-store";
            return File(data, "image/png");
        }

        /// <summary>The clan's live cape image; 404 when it has none (the game then makes one from the logo).</summary>
        [HttpGet("cape/{clanId}")]
        public ActionResult GetClanCape(Guid clanId)
        {
            var data = clanImages.GetLiveCape(clanId);
            if (data == null) return NotFound();
            Response.Headers["Cache-Control"] = "public, max-age=600";
            return File(data, "image/png");
        }

        /// <summary>
        ///     The clan page's editor sends the finished image here (a PNG as the body): kind is logo or cape.
        ///     The X-Ravenfall-Upload header makes a browser ask first when another site tries it.
        /// </summary>
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("images/{kind}")]
        [RequestSizeLimit(ClanImageManager.MaxUploadBytes + 1024)]
        public async Task<ActionResult<ClanImageResult>> UploadClanImage(string kind)
        {
            if (!TryKind(kind, out var k)) return BadRequest();
            if (!Request.Headers.ContainsKey("X-Ravenfall-Upload")) return BadRequest();
            var user = WebsiteUser();
            if (user == null) return Unauthorized();

            using var body = new MemoryStream();
            await Request.Body.CopyToAsync(body);
            return clanImages.Upload(user.Id, k, body.ToArray());
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("images/{kind}/default")]
        public ActionResult<ClanImageResult> UseDefaultClanImage(string kind)
        {
            if (!TryKind(kind, out var k)) return BadRequest();
            if (!Request.Headers.ContainsKey("X-Ravenfall-Upload")) return BadRequest();
            var user = WebsiteUser();
            if (user == null) return Unauthorized();
            return clanImages.UseDefault(user.Id, k);
        }

        private static bool TryKind(string kind, out ClanImageKind result)
            => Enum.TryParse(kind, true, out result) && Enum.IsDefined(result);

        /// <summary>The user signed in to the website, if any.</summary>
        private DataModels.User WebsiteUser()
        {
            var sessionId = HttpContext.GetSessionId();
            if (string.IsNullOrEmpty(sessionId) || !sessionInfoProvider.TryGet(sessionId, out var si) || !si.Authenticated) return null;
            return gameData.GetUser(si.UserId);
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

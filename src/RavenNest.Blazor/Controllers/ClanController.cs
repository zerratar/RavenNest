using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Game;
using RavenNest.Models;
using RavenNest.Sessions;
using System;

namespace RavenNest.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ClanController : GameApiController
    {
        private readonly IGameData gameData;
        private readonly IAuthManager authManager;
        private readonly ISessionInfoProvider sessionInfoProvider;
        private readonly ISessionManager sessionManager;
        private readonly IGameManager gameManager;
        private readonly IClanManager clanManager;
        private readonly ISecureHasher secureHasher;
        private readonly ILogger<ClanController> logger;

        public ClanController(
            ILogger<ClanController> logger,
            IGameData gameData,
            IAuthManager authManager,
            ISessionInfoProvider sessionInfoProvider,
            ISessionManager sessionManager,
            IGameManager gameManager,
            IClanManager clanManager,
            ISecureHasher secureHasher)
            : base(logger, gameData, authManager, sessionInfoProvider, sessionManager, secureHasher)
        {
            this.logger = logger;
            this.gameData = gameData;
            this.authManager = authManager;
            this.sessionInfoProvider = sessionInfoProvider;
            this.sessionManager = sessionManager;
            this.gameManager = gameManager;
            this.clanManager = clanManager;
            this.secureHasher = secureHasher;
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
        public bool ClanInvitePlayer(Guid senderCharacterId, Guid characterId)
        {
            var session = GetSessionToken();
            AssertSessionTokenValidity(session);

            return clanManager.SendPlayerInvite(characterId, senderCharacterId);
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
        public bool ClanInvitePlayerDecline(Guid characterId, string argument)
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
            return clanManager.JoinClan(clanOwnerId, characterId);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("leave/{characterId}")]
        public bool ClanPlayerLeave(Guid characterId)
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

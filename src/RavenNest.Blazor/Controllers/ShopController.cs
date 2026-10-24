using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Game;
using RavenNest.BusinessLogic.Game.StreamerShop;
using RavenNest.BusinessLogic.Net;
using RavenNest.Models;
using RavenNest.Sessions;
using System;

namespace RavenNest.Controllers
{
    /// <summary>
    /// The tavern shelves of the stream the session token belongs to. See StreamerShopManager.
    /// Everything acts on the session owner's shelves; buying acts for a character in that session.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class ShopController : GameApiController
    {
        private readonly StreamerShopManager shop;

        public ShopController(
            ILogger<ShopController> logger,
            GameData gameData,
            IAuthManager authManager,
            SessionInfoProvider sessionInfoProvider,
            SessionManager sessionManager,
            StreamerShopManager shop,
            ISecureHasher secureHasher)
            : base(logger, gameData, authManager, sessionInfoProvider, sessionManager, secureHasher)
        {
            this.shop = shop;
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet]
        public StreamerShopInfo GetShop()
        {
            var sessionToken = GetSessionToken();
            AssertSessionTokenValidity(sessionToken);
            return shop.GetShop(sessionToken.SessionId);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("sources")]
        public StreamerShopSources GetSources()
        {
            var sessionToken = GetSessionToken();
            AssertSessionTokenValidity(sessionToken);
            return shop.GetSources(sessionToken.SessionId);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("report")]
        public StreamerShopReport GetReport()
        {
            var sessionToken = GetSessionToken();
            AssertSessionTokenValidity(sessionToken);
            return shop.GetReport(sessionToken.SessionId);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("list")]
        public StreamerShopResult List([FromBody] StreamerShopListingRequest request)
        {
            var sessionToken = GetSessionToken();
            AssertSessionTokenValidity(sessionToken);
            return shop.List(sessionToken.SessionId, request);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("{id}/edit")]
        public StreamerShopResult Edit(Guid id, [FromBody] StreamerShopListingRequest request)
        {
            var sessionToken = GetSessionToken();
            AssertSessionTokenValidity(sessionToken);
            return shop.Edit(sessionToken.SessionId, id, request);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("{id}/remove")]
        public StreamerShopResult Remove(Guid id)
        {
            var sessionToken = GetSessionToken();
            AssertSessionTokenValidity(sessionToken);
            return shop.Remove(sessionToken.SessionId, id);
        }

        /// <summary>Buys from a listing for a character playing in the stream (a viewer's !redeem); one unless an amount is given.</summary>
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("{id}/buy/{characterId}/{amount?}")]
        public StreamerShopBuyResult Buy(Guid id, Guid characterId, long amount = 1)
        {
            var sessionToken = GetSessionToken();
            AssertSessionTokenValidity(sessionToken);
            return shop.Buy(sessionToken, characterId, id, amount);
        }
    }
}

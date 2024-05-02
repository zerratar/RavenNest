using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Game;
using RavenNest.Models;
using RavenNest.Sessions;

namespace RavenNest.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MarketplaceController : GameApiController
    {
        private readonly IAuthManager authManager;
        private readonly SessionManager sessionManager;
        private readonly SessionInfoProvider sessionInfoProvider;
        private readonly MarketplaceManager marketplace;

        public MarketplaceController(
            ILogger<MarketplaceController> logger,
            GameData gameData,
            IAuthManager authManager,
            SessionManager sessionManager,
            SessionInfoProvider sessionInfoProvider,
            MarketplaceManager marketplace,
            ISecureHasher secureHasher)
            : base(logger, gameData, authManager, sessionInfoProvider, sessionManager, secureHasher)
        {
            this.authManager = authManager;
            this.marketplace = marketplace;
            this.sessionManager = sessionManager;
            this.sessionInfoProvider = sessionInfoProvider;
        }

        [HttpGet("{offset}/{size}")]
        public MarketItemCollection Get(int offset, int size)
        {
            return this.marketplace.GetItems(offset, size);
        }

        [ApiExplorerSettings(IgnoreApi = true)]

        [HttpGet("{userId}/sell/{itemId}/{amount}/{pricePerItem}")]
        public ItemSellResult SellItem(string userId, Guid itemId, long amount, double pricePerItem)
        {
            var session = GetSessionToken();
            AssertSessionTokenValidity(session);
            return this.marketplace.SellItem(session, userId, "twitch", itemId, amount, pricePerItem);
        }

        [ApiExplorerSettings(IgnoreApi = true)]

        [HttpGet("{platform}/{userId}/sell/{itemId}/{amount}/{pricePerItem}")]
        public ItemSellResult SellItem(string userId, string platform, Guid itemId, long amount, double pricePerItem)
        {
            var session = GetSessionToken();
            AssertSessionTokenValidity(session);
            return this.marketplace.SellItem(session, userId, platform, itemId, amount, pricePerItem);
        }


        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("{userId}/buy/{itemId}/{amount}/{maxPricePerItem}")]
        public ItemBuyResult BuyItem(string userId, Guid itemId, long amount, double maxPricePerItem)
        {
            var session = GetSessionToken();
            AssertSessionTokenValidity(session);
            return this.marketplace.BuyItem(session, userId, "twitch", itemId, amount, maxPricePerItem);
        }

        [HttpGet("{platform}/{userId}/buy/{itemId}/{amount}/{maxPricePerItem}")]
        public ItemBuyResult BuyItem(string userId, string platform, Guid itemId, long amount, double maxPricePerItem)
        {
            var session = GetSessionToken();
            AssertSessionTokenValidity(session);
            return this.marketplace.BuyItem(session, userId, platform, itemId, amount, maxPricePerItem);
        }

        [HttpGet("v2/{characterId}/buy/{itemId}/{amount}/{maxPricePerItem}")]
        public ItemBuyResult BuyItem(Guid characterId, Guid itemId, long amount, double maxPricePerItem)
        {
            var session = GetSessionToken();
            AssertSessionTokenValidity(session);
            return this.marketplace.BuyItem(session, characterId, itemId, amount, maxPricePerItem);
        }


        [HttpGet("v2/{characterId}/sell/{itemId}/{amount}/{pricePerItem}")]
        public ItemSellResult SellItem(Guid characterId, Guid itemId, long amount, double pricePerItem)
        {
            var session = GetSessionToken();
            AssertSessionTokenValidity(session);
            return this.marketplace.SellItem(session, characterId, itemId, amount, pricePerItem);
        }

        [HttpGet("v2/value/{itemId}")]
        public ItemValueResult GetItemValue(Guid itemId)
        {
            return this.marketplace.GetItemValue(itemId, 0);
        }

        [HttpGet("v2/value/{itemId}/{amount}")]
        public ItemValueResult GetItemValue(Guid itemId, long amount)
        {
            return this.marketplace.GetItemValue(itemId, amount);
        }


    }
}

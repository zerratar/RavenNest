using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Docs.Attributes;
using RavenNest.BusinessLogic.Game;
using RavenNest.Models;
using RavenNest.Sessions;

namespace RavenNest.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [ApiDescriptor(Name = "Marketplace API", Description = "Used for buying and selling items in a global marketplace.")]
    public class MarketplaceController : GameApiController
    {
        private readonly IAuthManager authManager;
        private readonly SessionManager sessionManager;
        private readonly ISessionInfoProvider sessionInfoProvider;
        private readonly MarketplaceManager marketplace;

        public MarketplaceController(
            ILogger<MarketplaceController> logger,
            GameData gameData,
            IAuthManager authManager,
            SessionManager sessionManager,
            ISessionInfoProvider sessionInfoProvider,
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
        [MethodDescriptor(
            Name = "Get Marketplace Listing",
            Description = "Gets a range of items available on the marketplace based on a set offset and size.",
            RequiresSession = false,
            RequiresAuth = false)
        ]
        public MarketItemCollection Get(int offset, int size)
        {
            return this.marketplace.GetItems(offset, size);
        }

        [ApiExplorerSettings(IgnoreApi = true)]

        [HttpGet("{userId}/sell/{itemId}/{amount}/{pricePerItem}")]
        [MethodDescriptor(
            Name = "Sell items on the marketplace",
            Description = "Adds one or more item(s) on the marketplace listing for sale. This will remove the item(s) from the players inventory.",
            RequiresSession = true,
            RequiresAuth = false)
        ]
        public ItemSellResult SellItem(string userId, Guid itemId, long amount, double pricePerItem)
        {
            var session = GetSessionToken();
            AssertSessionTokenValidity(session);
            return this.marketplace.SellItem(session, userId, "twitch", itemId, amount, pricePerItem);
        }

        [ApiExplorerSettings(IgnoreApi = true)]

        [HttpGet("{platform}/{userId}/sell/{itemId}/{amount}/{pricePerItem}")]
        [MethodDescriptor(
            Name = "Sell items on the marketplace",
            Description = "Adds one or more item(s) on the marketplace listing for sale. This will remove the item(s) from the players inventory.",
            RequiresSession = true,
            RequiresAuth = false)
        ]
        public ItemSellResult SellItem(string userId, string platform, Guid itemId, long amount, double pricePerItem)
        {
            var session = GetSessionToken();
            AssertSessionTokenValidity(session);
            return this.marketplace.SellItem(session, userId, platform, itemId, amount, pricePerItem);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("{userId}/buy/{itemId}/{amount}/{maxPricePerItem}")]
        [MethodDescriptor(
            Name = "Buy items on the marketplace",
            Description = "Buy the target item(s) with the cheapest price per item, this price cannot exceed the requested max price per item. The bought item(s) will be equipped automatically if they are better than the currently equipped item of same type.",
            RequiresSession = true,
            RequiresAuth = false)
        ]
        public ItemBuyResult BuyItem(string userId, Guid itemId, long amount, double maxPricePerItem)
        {
            var session = GetSessionToken();
            AssertSessionTokenValidity(session);
            return this.marketplace.BuyItem(session, userId, "twitch", itemId, amount, maxPricePerItem);
        }

        [HttpGet("{platform}/{userId}/buy/{itemId}/{amount}/{maxPricePerItem}")]
        [MethodDescriptor(
          Name = "Buy items on the marketplace",
          Description = "Buy the target item(s) with the cheapest price per item, this price cannot exceed the requested max price per item. The bought item(s) will be equipped automatically if they are better than the currently equipped item of same type.",
          RequiresSession = true,
          RequiresAuth = false)
      ]
        public ItemBuyResult BuyItem(string userId, string platform, Guid itemId, long amount, double maxPricePerItem)
        {
            var session = GetSessionToken();
            AssertSessionTokenValidity(session);
            return this.marketplace.BuyItem(session, userId, platform, itemId, amount, maxPricePerItem);
        }
    }
}

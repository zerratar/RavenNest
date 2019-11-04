using System;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Mvc;
using RavenNest.BusinessLogic.Docs.Attributes;
using RavenNest.BusinessLogic.Game;
using RavenNest.Models;
using RavenNest.Sessions;

namespace RavenNest.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [ApiDescriptor(Name = "Marketplace API", Description = "Used for buying and selling items in a global marketplace.")]
    public class MarketplaceController : ControllerBase
    {
        private readonly IAuthManager authManager;
        private readonly ISessionManager sessionManager;
        private readonly ISessionInfoProvider sessionInfoProvider;
        private readonly IMarketplaceManager marketplace;

        public MarketplaceController(
            IAuthManager authManager,
            ISessionManager sessionManager,
            ISessionInfoProvider sessionInfoProvider,
            IMarketplaceManager marketplace)
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
            RequiresAuth = true)
        ]
        public MarketItemCollection Get(int offset, int size)
        {
            var auth = GetAuthToken();
            AssertAuthTokenValidity(auth);
            return this.marketplace.GetItems(offset, size);
        }

        [HttpGet("{userId}/sell/{itemId}/{amount}/{pricePerItem}")]
        [MethodDescriptor(
            Name = "Sell items on the marketplace",
            Description = "Adds one or more item(s) on the marketplace listing for sale. This will remove the item(s) from the players inventory.",
            RequiresSession = true,
            RequiresAuth = false)
        ]
        public ItemSellResult SellItem(
            string userId,
            Guid itemId,
            long amount,
            decimal pricePerItem)
        {
            var session = GetSessionToken();
            AssertSessionTokenValidity(session);
            return this.marketplace.SellItem(session, userId, itemId, amount, pricePerItem);
        }

        [HttpGet("{userId}/buy/{itemId}/{amount}/{maxPricePerItem}")]
        [MethodDescriptor(
            Name = "Buy items on the marketplace",
            Description = "Buy the target item(s) with the cheapest price per item, this price cannot exceed the requested max price per item. The bought item(s) will be equipped automatically if they are better than the currently equipped item of same type.",
            RequiresSession = true,
            RequiresAuth = false)
        ]
        public ItemBuyResult BuyItem(
            string userId,
            Guid itemId,
            long amount,
            decimal maxPricePerItem)
        {
            var session = GetSessionToken();
            AssertSessionTokenValidity(session);
            return this.marketplace.BuyItem(session, userId, itemId, amount, maxPricePerItem);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void AssertAuthTokenValidity(AuthToken authToken)
        {
            if (authToken == null) throw new NullReferenceException(nameof(authToken));
            if (authToken.UserId == Guid.Empty) throw new NullReferenceException(nameof(authToken.UserId));
            if (authToken.Expired) throw new Exception("Session has expired.");
        }

        private SessionToken GetSessionToken()
        {
            return HttpContext.Request.Headers.TryGetValue("session-token", out var value)
                ? sessionManager.Get(value)
                : null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void AssertSessionTokenValidity(SessionToken sessionToken)
        {
            if (sessionToken == null) throw new NullReferenceException(nameof(sessionToken));
            if (string.IsNullOrEmpty(sessionToken.AuthToken)) throw new NullReferenceException(nameof(sessionToken.AuthToken));
            if (sessionToken.Expired) throw new Exception("Session has expired.");
        }

        private AuthToken GetAuthToken()
        {
            if (HttpContext.Request.Headers.TryGetValue("auth-token", out var value))
            {
                return authManager.Get(value);
            }

            if (sessionInfoProvider.TryGetAuthToken(HttpContext.Session, out var authToken))
            {
                return authToken;
            }

            return null;
        }
    }
}
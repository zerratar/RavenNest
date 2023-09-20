using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Game;
using RavenNest.Models;
using RavenNest.Sessions;

namespace RavenNest.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ItemsController : ControllerBase
    {
        private readonly GameData gameData;
        private readonly SessionInfoProvider sessionInfoProvider;
        private readonly ItemManager itemManager;
        private readonly IAuthManager authManager;

        public ItemsController(
            GameData gameData,
            SessionInfoProvider sessionInfoProvider,
            ItemManager itemManager,
            IAuthManager authManager)
        {
            this.gameData = gameData;
            this.sessionInfoProvider = sessionInfoProvider;
            this.itemManager = itemManager;
            this.authManager = authManager;
        }

        /// <summary>
        /// Get all available items
        /// </summary>
        /// <returns>This will return the list of all available items in Ravenfall. This is required as no other endpoints will give out any item data other than item id. This list of items is then necessary to do an item lookup.</returns>
        [HttpGet]
        public async Task<ActionResult<ItemCollection>> Get()
        {
            if (itemManager == null)
            {
                return new ItemCollection();
            }

            var itemCollection = itemManager.GetAllItems();
            return itemCollection;
        }

        [HttpGet("delta/{timestamp}")]
        public async Task<ActionResult<ItemCollection>> Get(DateTime timestamp)
        {
            if (itemManager == null)
            {
                return new ItemCollection();
            }

            var itemCollection = itemManager.GetAllItems(timestamp);
            return itemCollection;
        }

        /// <summary>
        /// Get all resource drops
        /// </summary>
        /// <returns>This will return the list of all redeemable items in Ravenfall.</returns>
        [HttpGet("drops")]
        public async Task<ActionResult<ResourceItemDropCollection>> GetResourceDrops()
        {
            if (itemManager == null)
            {
                return new ResourceItemDropCollection();
            }

            var itemCollection = itemManager.GetResourceItemDrops();
            return itemCollection;
        }


        /// <summary>
        /// Get all redeemable items
        /// </summary>
        /// <returns>This will return the list of all redeemable items in Ravenfall.</returns>
        [HttpGet("redeemable")]
        public async Task<ActionResult<RedeemableItemCollection>> GetRedeemables()
        {
            if (itemManager == null)
            {
                return new RedeemableItemCollection();
            }

            var itemCollection = itemManager.GetRedeemableItems();
            return itemCollection;
        }

        [HttpGet("recipes")]
        public async Task<ActionResult<ItemRecipeCollection>> GetRecipes()
        {
            if (itemManager == null)
            {
                return new ItemRecipeCollection();
            }

            return itemManager.GetRecipes();
        }



        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost]
        public bool AddItemAsync(Item item)
        {
            var authToken = GetAuthToken();
            AssertAdminAuthToken(authToken);
            return this.itemManager.TryAddItem(item);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpDelete("{itemId}")]
        public bool RemoveItem(Guid itemId)
        {
            var authToken = GetAuthToken();
            AssertAdminAuthToken(authToken);
            return this.itemManager.RemoveItem(itemId);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPut]
        public bool UpdateItem(Item item)
        {
            var authToken = GetAuthToken();
            AssertAdminAuthToken(authToken);
            return this.itemManager.TryUpdateItem(item);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AssertAdminAuthToken(AuthToken authToken)
        {
            var user = gameData.GetUser(authToken.UserId);
            if (!user.IsAdmin.GetValueOrDefault())
                throw new Exception("You do not have permissions to call this API");
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void AssertAuthTokenValidity(AuthToken authToken)
        {
            if (authToken == null) throw new NullReferenceException(nameof(authToken));
            if (authToken.UserId == Guid.Empty) throw new NullReferenceException(nameof(authToken.UserId));
            if (authToken.Expired) throw new Exception("Session has expired.");
        }

        private AuthToken GetAuthToken()
        {
            if (HttpContext.Request.Headers.TryGetValue("auth-token", out var value))
            {
                return authManager.Get(value);
            }

            if (sessionInfoProvider.TryGetAuthToken(HttpContext.GetSessionId(), out var authToken))
            {
                return authToken;
            }

            return null;
        }
    }
}

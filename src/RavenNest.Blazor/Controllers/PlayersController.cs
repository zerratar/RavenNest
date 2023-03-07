using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RavenNest.BusinessLogic;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Docs.Attributes;
using RavenNest.BusinessLogic.Extended;
using RavenNest.BusinessLogic.Game;
using RavenNest.BusinessLogic.Net;
using RavenNest.BusinessLogic.Providers;
using RavenNest.Models;
using RavenNest.Sessions;

namespace RavenNest.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [ApiDescriptor(Name = "Players API", Description = "Used for managing player data.")]
    public class PlayersController : GameApiController
    {
        private readonly ISessionInfoProvider sessionInfoProvider;
        private readonly SessionManager sessionManager;
        private readonly PlayerManager playerManager;
        private readonly IRavenfallDbContextProvider dbProvider;
        private readonly IOptions<AppSettings> settings;
        private readonly ISecureHasher secureHasher;
        private readonly IAuthManager authManager;

        public PlayersController(
            ILogger<PlayersController> logger,
            GameData gameData,
            ISessionInfoProvider sessionInfoProvider,
            PlayerInventoryProvider inventoryProvider,
            SessionManager sessionManager,
            PlayerManager playerManager,
            IRavenfallDbContextProvider dbProvider,
            ISecureHasher secureHasher,
            IAuthManager authManager,
            IOptions<AppSettings> settings)
            : base(logger, gameData, authManager, sessionInfoProvider, sessionManager, secureHasher)
        {
            this.sessionInfoProvider = sessionInfoProvider;
            this.sessionManager = sessionManager;
            this.playerManager = playerManager;
            this.dbProvider = dbProvider;
            this.secureHasher = secureHasher;
            this.authManager = authManager;
            this.settings = settings;
        }

        [HttpGet]
        [MethodDescriptor(
            Name = "Get Current Player",
            Description = "Gets the player data for the authenticated Twitch user, authenticated RavenNest user or current Game Session user.")]
        public Task<Player> Get()
        {
            return GetPlayerAsync();
        }

        [HttpGet("all")]
        public Task<IReadOnlyList<WebsitePlayer>> GetAllMyPlayers()
        {
            return GetPlayersAsync();
        }

        [HttpGet("user")] // due to a misspelling in the customization tool. god darnit :P
        [MethodDescriptor(Name = "(Alias) Get Current Player", Description = "Gets the player data for the authenticated Twitch user, authenticated RavenNest user or current Game Session user.")]

        public Task<Player> GetUser()
        {
            return GetPlayerAsync();
        }
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("{userId}")]
        //[MethodDescriptor(Name = "Add Player to Game Session", Description = "Adds the target player to the ongoing session. This will lock the target player to the session and then return the player data.", RequiresSession = true)]
        public Task<PlayerJoinResult> PlayerJoin(string userId, Single<string> username)
        {
            return playerManager.AddPlayer(AssertGetSessionToken(), userId, username.Value);
        }
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("{userId}/{identifier}")]
        //[MethodDescriptor(Name = "Add Player to Game Session", Description = "Adds the target player to the ongoing session. This will lock the target player to the session and then return the player data.", RequiresSession = true)]
        public Task<PlayerJoinResult> PlayerJoin(string userId, string identifier, Single<string> username)
        {
            return playerManager.AddPlayer(AssertGetSessionToken(), userId, username.Value, identifier);
        }
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("loyalty")]
        public bool SendLoyalty([FromBody] LoyaltyUpdate data)
        {
            return playerManager.AddLoyaltyData(AssertGetSessionToken(), data);
        }
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost]
        public Task<PlayerJoinResult> PlayerJoin([FromBody] PlayerJoinData playerData)
        {
            return playerManager.AddPlayer(AssertGetSessionToken(), playerData);
        }
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("restore")]
        public Task<PlayerRestoreResult> Restore([FromBody] PlayerRestoreData players)
        {
            //var res = new JsonResult(players, new JsonSerializerSettings()
            //{
            //    NullValueHandling = NullValueHandling.Ignore
            //});
            return playerManager.RestorePlayersToGame(AssertGetSessionToken(), players);
        }
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpDelete("{characterId}")]
        public Task<bool> RemovePlayer(Guid characterId)
        {
            return playerManager.RemovePlayerFromActiveSession(AssertGetSessionToken(), characterId);
        }

        [HttpGet("{userId}")]
        [MethodDescriptor(
            Name = "Get Player by Twitch UserId",
            Description = "Get the target player using a Twitch UserId. This requires a session token for grabbing a local player but only an auth token for a global player.",
            RequiresAuth = true)
        ]
        public Player GetPlayer(string userId)
        {
            if (GetSessionToken() == null)
            {
                AssertAuthTokenValidity(GetAuthToken());
                return playerManager.GetPlayer(userId, "1");
            }

            return playerManager.GetPlayer(AssertGetSessionToken(), userId);
        }


        [HttpGet("{userId}/{identifier}")]
        [MethodDescriptor(
            Name = "Get Player by Twitch UserId",
            Description = "Get the target player using a Twitch UserId. This requires a session token for grabbing a local player but only an auth token for a global player.",
            RequiresAuth = true)]
        public Player GetPlayer(string userId, string identifier)
        {
            if (GetSessionToken() == null)
            {
                AssertAuthTokenValidity(GetAuthToken());
                return playerManager.GetPlayer(userId, identifier);
            }

            return playerManager.GetPlayer(AssertGetSessionToken(), userId);
        }
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("highscore/{characterId}/{skillName}")]
        public int GetHighscore(Guid characterId, string skillName)
        {
            return playerManager.GetHighscore(AssertGetSessionToken(), characterId, skillName);
        }
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("{userId}/redeem-tokens/{amount}/{exact}")]
        public int RedeemTokens(string userId, int amount, bool exact)
        {
            return playerManager.RedeemTokens(AssertGetSessionToken(), userId, amount, exact);
        }
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("{characterId}/redeem-item/{itemId}")]
        public RedeemItemResult RedeemItem(Guid characterId, Guid itemId)
        {
            return playerManager.RedeemItem(AssertGetSessionToken(), characterId, itemId);
        }
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("{userId}/add-tokens/{amount}")]
        public bool AddTokens(string userId, int amount)
        {
            return playerManager.AddTokens(AssertGetSessionToken(), userId, amount);
        }
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("{userId}/craft-many/{item}/{amount}")]
        public CraftItemResult CraftItems(string userId, Guid item, int amount)
        {
            return playerManager.CraftItems(AssertGetSessionToken(), userId, item, amount);
        }
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("{userId}/enchant-item/{inventoryItemId}")]
        public ItemEnchantmentResult EnchantItem(string userId, Guid inventoryItemId)
        {
            return playerManager.EnchantItem(AssertGetSessionToken(), userId, inventoryItemId);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("{userId}/enchant-instance/{inventoryItemId}")]
        public ItemEnchantmentResult EnchantItemInstance(string userId, Guid inventoryItemId)
        {
            return playerManager.EnchantItemInstance(AssertGetSessionToken(), userId, inventoryItemId);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("{userId}/disenchant-instance/{inventoryItemId}")]
        public ItemEnchantmentResult DisenchantItemInstance(string userId, Guid inventoryItemId)
        {
            return playerManager.DisenchantItemInstance(AssertGetSessionToken(), userId, inventoryItemId);
        }
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("{userId}/item/{item}")]
        public AddItemResult AddItem(string userId, Guid item)
        {
            return playerManager.AddItem(AssertGetSessionToken(), userId, item, out _);
        }
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("{userId}/item-instance")]
        public Guid AddItemInstance(string userId, [FromBody] RavenNest.Models.InventoryItem instance)
        {
            return playerManager.AddItemInstance(AssertGetSessionToken(), userId, instance);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("{userId}/item-instance-detailed")]
        public AddItemInstanceResult AddItemInstanceDetailed(string userId, [FromBody] RavenNest.Models.InventoryItem instance)
        {
            return playerManager.AddItemInstanceDetailed(AssertGetSessionToken(), userId, instance);
        }
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("{userId}/toggle-helmet")]
        public bool UnEquipItem(string userId)
        {
            return playerManager.ToggleHelmet(AssertGetSessionToken(), userId);
        }
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("{userId}/unequip/{item}")]
        public bool UnEquipItem(string userId, Guid item)
        {
            return playerManager.UnequipItem(AssertGetSessionToken(), userId, item);
        }
        [ApiExplorerSettings(IgnoreApi = true)]

        [HttpGet("{userId}/unequip-instance/{inventoryItemId}")]
        public bool UnEquipItemInstance(string userId, Guid inventoryItemId)
        {
            return playerManager.UnequipItemInstance(AssertGetSessionToken(), userId, inventoryItemId);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("{userId}/unequipall")]
        public bool UnequipAllItems(string userId)
        {
            return playerManager.UnequipAllItems(AssertGetSessionToken(), userId);
        }
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("{userId}/equip-instance/{item}")]
        public bool EquipItemInstance(string userId, Guid inventoryItemId)
        {
            return playerManager.EquipItemInstance(AssertGetSessionToken(), userId, inventoryItemId);
        }
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("{userId}/equip/{item}")]
        public bool EquipItem(string userId, Guid item)
        {
            return playerManager.EquipItem(AssertGetSessionToken(), userId, item);
        }
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("{userId}/equipall")]
        public bool EquipBestItems(string userId)
        {
            return playerManager.EquipBestItems(AssertGetSessionToken(), userId);
        }
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("{userId}/gift/{receiverUserId}/{itemId}/{amount}")]
        public long GiftItem(string userId, string receiverUserId, Guid itemId, long amount)
        {
            return playerManager.GiftItem(AssertGetSessionToken(), userId, receiverUserId, itemId, amount);
        }
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("{userId}/gift-instance/{receiverUserId}/{itemId}/{amount}")]
        public long GiftItemInstance(string userId, string receiverUserId, Guid itemId, long amount)
        {
            return playerManager.GiftItemInstance(AssertGetSessionToken(), userId, receiverUserId, itemId, amount);
        }
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("{userId}/vendor/{item}/{amount}")]
        public long VendorItem(string userId, Guid item, long amount)
        {
            return playerManager.VendorItem(AssertGetSessionToken(), userId, item, amount);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("{userId}/vendor-instance/{item}/{amount}")]
        public long VendorItemInstance(string userId, Guid item, long amount)
        {
            return playerManager.VendorItemInstance(AssertGetSessionToken(), userId, item, amount);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("{userId}/appearance")]
        public async Task<bool> UpdateSyntyAppearanceAsync(string userId, SyntyAppearance appearance)
        {
            userId = CleanupUserId(userId);
            var sessionId = HttpContext.GetSessionId();
            if (sessionInfoProvider.TryGet(sessionId, out var si) && si.ActiveCharacterId != null)
            {
                return playerManager.UpdateAppearance(si.ActiveCharacterId.Value, appearance);
            }

            return await UpdateSyntyAppearanceAsync(userId, "1", appearance);
        }
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("{userId}/{identifier}/appearance")]
        public async Task<bool> UpdateSyntyAppearanceAsync(string userId, string identifier, SyntyAppearance appearance)
        {
            userId = CleanupUserId(userId); // we get a weird input sent from the client. This shouldnt 
                                            // be fixed here, but as a temporary bugfix
            var sessionId = HttpContext.GetSessionId();
            var twitchUserSession = await sessionInfoProvider.GetTwitchUserAsync(sessionId);
            if (twitchUserSession != null)
            {
                return playerManager.UpdateAppearance(userId, identifier, appearance);
            }

            var authToken = GetAuthToken();
            if (authToken != null)
            {
                return playerManager.UpdateAppearance(authToken, userId, identifier, appearance);
            }

            return playerManager.UpdateAppearance(AssertGetSessionToken(), userId, appearance);
        }
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("{userId}/statistics")]
        public bool UpdateStatistics(string userId, Many<double> statistics)
        {
            return playerManager.UpdateStatistics(AssertGetSessionToken(), userId, statistics.Values);
        }

        //[HttpPost("{userId}/resources")]
        //public bool UpdateResources(string userId, Many<decimal> resources)
        //{
        //    return playerManager.UpdateResources(AssertGetSessionToken(), userId, resources.Values);
        //}

        //[HttpPost("update")]
        //public bool[] UpdateMany(Many<PlayerState> states)
        //{
        //    return playerManager.UpdateMany(AssertGetSessionToken(), states.Values);
        //}

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("extended/{identifier}")]
        public async Task<WebsitePlayer> GetPlayerExtendedAsync(string identifier)
        {
            var twitchUser = await sessionInfoProvider.GetTwitchUserAsync(HttpContext.GetSessionId());
            if (twitchUser != null)
            {
                return playerManager.GetWebsitePlayer(twitchUser.Id.ToString(), identifier);
            }

            var sessionToken = GetSessionToken();
            if (sessionToken == null || sessionToken.Expired || string.IsNullOrEmpty(sessionToken.AuthToken))
            {
                var auth = GetAuthToken();
                if (auth != null && !auth.Expired)
                {
                    return playerManager.GetWebsitePlayer(auth.UserId, identifier);
                }

            }

            return null;
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AssertAuthTokenValidity(AuthToken authToken)
        {
            if (authToken == null) throw new NullReferenceException(nameof(authToken));
            if (authToken.UserId == Guid.Empty) throw new NullReferenceException(nameof(authToken.UserId));
            if (authToken.Expired) throw new Exception("Session has expired.");
            if (string.IsNullOrEmpty(authToken.Token)) throw new Exception("Session has expired.");
            if (authToken.Token != secureHasher.Get(authToken))
            {
                throw new Exception("Session has expired.");
            }
        }

        private async Task<System.Collections.Generic.IReadOnlyList<WebsitePlayer>> GetPlayersAsync()
        {
            var sessionId = HttpContext.GetSessionId();

            if (sessionInfoProvider.TryGet(sessionId, out var si))
            {
                return playerManager.GetWebsitePlayers(si.AccountId);
            }

            return new System.Collections.Generic.List<WebsitePlayer>();
        }

        private async Task<Player> GetPlayerAsync()
        {
            var sessionId = HttpContext.GetSessionId();

            if (sessionInfoProvider.TryGet(sessionId, out var si) && si.ActiveCharacterId != null)
            {
                return playerManager.GetPlayer(si.ActiveCharacterId.Value);
            }

            var twitchUser = await sessionInfoProvider.GetTwitchUserAsync(HttpContext.GetSessionId());
            if (twitchUser != null)
            {
                return playerManager.GetPlayer(twitchUser.Id.ToString(), "1");
            }

            var sessionToken = GetSessionToken();
            if (sessionToken == null || sessionToken.Expired || string.IsNullOrEmpty(sessionToken.AuthToken))
            {
                var auth = GetAuthToken();
                if (auth != null && !auth.Expired)
                {
                    return playerManager.GetPlayer(auth.UserId, "1");
                }

                return null;
            }

            return playerManager.GetPlayer(sessionToken);
        }

        private SessionToken AssertGetSessionToken()
        {
            var sessionToken = GetSessionToken();
            AssertSessionTokenValidity(sessionToken);
            return sessionToken;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private string CleanupUserId(string userId)
        {
            return userId.Replace("}", "").Trim();
        }
    }
}

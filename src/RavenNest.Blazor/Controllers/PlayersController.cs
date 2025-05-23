﻿using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RavenNest.BusinessLogic;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Extended;
using RavenNest.BusinessLogic.Game;
using RavenNest.Models;
using System.IO;
using RavenNest.Blazor.Services;

namespace RavenNest.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PlayersController : GameApiController
    {
        private readonly LogoService logoService;
        private readonly SessionInfoProvider sessionInfoProvider;
        private readonly SessionManager sessionManager;
        private readonly PlayerManager playerManager;
        private readonly IRavenfallDbContextProvider dbProvider;
        private readonly IOptions<AppSettings> settings;
        private readonly ISecureHasher secureHasher;
        private readonly IAuthManager authManager;

        private readonly byte[] unknownProfilePictureBytes;
        private readonly string unknownProfilePictureUrl;

        public PlayersController(
            ILogger<PlayersController> logger,
            GameData gameData,
            LogoService logoService,
            SessionInfoProvider sessionInfoProvider,
            SessionManager sessionManager,
            PlayerManager playerManager,

            IRavenfallDbContextProvider dbProvider,
            ISecureHasher secureHasher,
            IAuthManager authManager,
            IOptions<AppSettings> settings)
            : base(logger, gameData, authManager, sessionInfoProvider, sessionManager, secureHasher)
        {
            this.logoService = logoService;
            this.sessionInfoProvider = sessionInfoProvider;
            this.sessionManager = sessionManager;
            this.playerManager = playerManager;
            this.dbProvider = dbProvider;
            this.secureHasher = secureHasher;
            this.authManager = authManager;
            this.settings = settings;

            var a = unknownProfilePictureUrl = "imgs/ravenfall_logo_tiny.png";
            if (!System.IO.File.Exists(a))
                a = Path.Combine("wwwroot", a);

            if (System.IO.File.Exists(a))
            {
                this.unknownProfilePictureBytes = System.IO.File.ReadAllBytes(a);
            }
        }

        [HttpGet("logo/{userId}")]
        [ResponseCache(VaryByHeader = "User-Agent", Duration = 600)]
        public async Task<ActionResult> GetChannelPictureAsync(Guid userId)
        {
            try
            {
                //var user = GameData.GetUser(userId);
                var twitchUserAccess = GameData.GetUserAccess(userId, "twitch");
                if (twitchUserAccess != null)
                {
                    var imageData = await logoService.GetChannelPictureAsync(twitchUserAccess.PlatformId);
                    if (imageData != null)
                    {
                        return File(imageData, "image/png");
                    }
                }

                if (unknownProfilePictureBytes == null)
                {
                    return NotFound();
                }

                return File(unknownProfilePictureBytes, "image/png");
            }
            catch { }
            return NotFound();
        }



        [HttpGet]
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
        public Task<Player> GetUser()
        {
            return GetPlayerAsync();
        }


        [HttpGet("user/min")] // due to a misspelling in the customization tool. god darnit :P
        public Task<Player> GetUserMin()
        {
            return GetPlayerInfoAsync();
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("{userId}")]
        [Obsolete]
        public Task<PlayerJoinResult> PlayerJoin(string userId, Single<string> username)
        {
            return playerManager.AddPlayer(AssertGetSessionToken(), userId, username.Value, "twitch");
        }


        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("{userId}/{identifier}")]
        public Task<PlayerJoinResult> PlayerJoin(string userId, string identifier, Single<string> username)
        {
            return playerManager.AddPlayer(AssertGetSessionToken(), userId, username.Value, "twitch", identifier);
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
        [HttpPost("remove-failed/{characterId}")]
        public Task<bool> PlayerRemoveFailed(Guid characterId, [FromBody] string reason)
        {
            return playerManager.PlayerRemoveFailed(AssertGetSessionToken(), characterId, reason);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("restore")]
        public Task<PlayerRestoreResult> Restore([FromBody] PlayerRestoreData players)
        {
            return playerManager.RestorePlayersToGame(AssertGetSessionToken(), players);
        }
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpDelete("{characterId}")]
        public Task<bool> RemovePlayer(Guid characterId)
        {
            return playerManager.RemovePlayerFromActiveSession(AssertGetSessionToken(), characterId);
        }

        [HttpGet("{characterId}")]
        public Player GetPlayer(Guid characterId)
        {
            AssertAuthTokenValidity(GetAuthToken());
            return playerManager.GetPlayer(characterId);
        }

        [HttpGet("twitch/{userId}")]
        public Player GetPlayerByTwitchUser(string userId)
        {
            if (GetSessionToken() == null)
            {
                AssertAuthTokenValidity(GetAuthToken());
                return playerManager.GetPlayer(userId, "twitch", "1");
            }

            return playerManager.GetPlayer(AssertGetSessionToken(), userId);
        }

        [HttpGet("twitch/{userId}/{identifier}")]
        public Player GetPlayerByTwitchUser(string userId, string identifier)
        {
            if (GetSessionToken() == null)
            {
                AssertAuthTokenValidity(GetAuthToken());
                return playerManager.GetPlayer(userId, "twitch", identifier);
            }

            return playerManager.GetPlayer(AssertGetSessionToken(), userId);
        }

        [HttpGet("v2/{userId}/{identifier}")]
        public Player GetPlayerByUserId(Guid userId, string identifier)
        {
            AssertAuthTokenValidity(GetAuthToken());
            return playerManager.GetPlayer(userId, identifier);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("highscore/{characterId}/{skillName}")]
        public int GetHighscore(Guid characterId, string skillName)
        {
            return playerManager.GetHighscore(AssertGetSessionToken(), characterId, skillName);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("{characterId}/redeem-item/{itemId}")]
        public RedeemItemResult RedeemItem(Guid characterId, Guid itemId)
        {
            return playerManager.RedeemItem(AssertGetSessionToken(), characterId, itemId);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("{userId}/add-tokens/{amount}")]
        [Obsolete]
        public bool AddTokens(string userId, int amount)
        {
            return playerManager.AddTokens(AssertGetSessionToken(), userId, amount);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("v2/{characterId}/add-tokens/{amount}")]
        public bool AddTokens(Guid characterId, int amount)
        {
            return playerManager.AddTokens(AssertGetSessionToken(), characterId, amount);
        }


        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("{characterId}/produce/{recipeId}/{amount}")]
        public ItemProductionResult ProduceItems(Guid characterId, Guid recipeId, int amount)
        {
            return playerManager.ProduceItems(AssertGetSessionToken(), characterId, recipeId, amount);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("{userId}/craft-many/{item}/{amount}")]
        public CraftItemResult CraftItems(string userId, Guid item, int amount)
        {
            return playerManager.CraftItems(AssertGetSessionToken(), userId, item, amount);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("v2/{characterId}/craft/{item}/{amount}")]
        public CraftItemResult CraftItems(Guid characterId, Guid item, int amount)
        {
            return playerManager.CraftItems(AssertGetSessionToken(), characterId, item, amount);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("{userId}/enchant-item/{inventoryItemId}")]
        public ItemEnchantmentResult EnchantItem(string userId, Guid inventoryItemId)
        {
            return playerManager.EnchantItem(AssertGetSessionToken(), userId, inventoryItemId);
        }

        [Obsolete]
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("{userId}/enchant-instance/{inventoryItemId}")]
        public ItemEnchantmentResult EnchantItemInstance(string userId, Guid inventoryItemId)
        {
            return playerManager.EnchantItemInstance(AssertGetSessionToken(), userId, inventoryItemId);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("v2/{characterId}/enchant-instance/{inventoryItemId}")]
        public ItemEnchantmentResult EnchantItemInstance(Guid characterId, Guid inventoryItemId)
        {
            return playerManager.EnchantItemInstance(AssertGetSessionToken(), characterId, inventoryItemId);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("v2/{characterId}/disenchant-instance/{inventoryItemId}")]
        public ItemEnchantmentResult DisenchantItemInstance(Guid characterId, Guid inventoryItemId)
        {
            return playerManager.DisenchantItemInstance(AssertGetSessionToken(), characterId, inventoryItemId);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("{userId}/disenchant-instance/{inventoryItemId}")]
        public ItemEnchantmentResult DisenchantItemInstance(string userId, Guid inventoryItemId)
        {
            return playerManager.DisenchantItemInstance(AssertGetSessionToken(), userId, inventoryItemId);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("{userId}/item/{item}")]
        [Obsolete]
        public AddItemResult AddItem(string userId, Guid item)
        {
            return playerManager.AddItem(AssertGetSessionToken(), userId, item, out _);
        }


        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("v2/{characterId}/item/{item}")]
        public AddItemResult AddItem(Guid characterId, Guid item)
        {
            return playerManager.AddItem(AssertGetSessionToken(), characterId, item, out _);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("v2/{characterId}/item")]
        public AddItemInstanceResult AddItemInstanceDetailed(Guid characterId, [FromBody] AddItemRequest instance)
        {
            return playerManager.AddItem(AssertGetSessionToken(), characterId, instance);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("{userId}/item-instance")]
        public Guid AddItemInstance(string userId, [FromBody] AddItemRequest instance)
        {
            return playerManager.AddItemInstance(AssertGetSessionToken(), userId, instance);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("{userId}/item-instance-detailed")]
        public AddItemInstanceResult AddItemInstanceDetailed(string userId, [FromBody] AddItemRequest instance)
        {
            return playerManager.AddItemInstanceDetailed(AssertGetSessionToken(), userId, instance);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("{userId}/toggle-helmet")]
        [Obsolete]
        public bool UnEquipItem(string userId)
        {
            return playerManager.ToggleHelmet(AssertGetSessionToken(), userId);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("v2/{characterId}/toggle-helmet")]
        public bool UnEquipItem(Guid characterId)
        {
            return playerManager.ToggleHelmet(AssertGetSessionToken(), characterId);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("{userId}/unequip/{item}")]
        [Obsolete]
        public bool UnEquipItem(string userId, Guid item)
        {
            return playerManager.UnequipItem(AssertGetSessionToken(), userId, item);
        }
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("v2/{characterId}/unequip/{item}")]
        public bool UnEquipItem(Guid characterId, Guid item)
        {
            return playerManager.UnequipItem(AssertGetSessionToken(), characterId, item);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("{characterId}/use/{inventoryItemId}/{arg}")]
        public ItemUseResult UseItemWithArgs(Guid characterId, Guid inventoryItemId, string arg)
        {
            return playerManager.UseItem(AssertGetSessionToken(), characterId, inventoryItemId, arg);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("{characterId}/use/{inventoryItemId}")]
        public ItemUseResult UseItem(Guid characterId, Guid inventoryItemId)
        {
            return playerManager.UseItem(AssertGetSessionToken(), characterId, inventoryItemId, null);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("{characterId}/clear-enchantment-cooldown")]
        public ClearEnchantmentCooldownResult ClearEnchantmentCooldown(Guid characterId)
        {
            return playerManager.ClearEnchantmentCooldown(AssertGetSessionToken(), characterId);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("{characterId}/enchantment-cooldown")]
        public EnchantmentCooldownResult GetEnchantmentCooldown(Guid characterId)
        {
            return playerManager.GetEnchantmentCooldown(AssertGetSessionToken(), characterId);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("{characterId}/raid-auto")]
        public bool RaidAuto(Guid characterId)
        {
            return playerManager.AutoJoinRaid(AssertGetSessionToken(), characterId);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("raid-auto")]
        public bool[] RaidAuto(Many<Guid> characterIds)
        {
            try
            {
                var session = GetSessionToken();
                AssertSessionTokenValidity(session);
                return playerManager.AutoJoinRaid(session, characterIds.Values);
            }
            catch (Exception exc)
            {
                logger.LogError("Error when auto-joining raid: " + exc);
                return new bool[characterIds.Values.Length];
            }
        }


        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("dungeon-auto-cost")]
        public int DungeonAutoCost()
        {
            return PlayerManager.AutoJoinDungeonCost;
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("raid-auto-cost")]
        public int RaidAutoCost()
        {
            return PlayerManager.AutoJoinRaidCost;
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("{characterId}/dungeon-auto")]
        public bool DungeonAuto(Guid characterId)
        {
            return playerManager.AutoJoinDungeon(AssertGetSessionToken(), characterId);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("dungeon-auto")]
        public bool[] DungeonAuto(Many<Guid> characterIds)
        {
            try
            {
                var session = GetSessionToken();
                AssertSessionTokenValidity(session);
                return playerManager.AutoJoinDungeon(session, characterIds.Values);
            }
            catch (Exception exc)
            {
                logger.LogError("Error when auto-joining dungeon: " + exc);
                return new bool[characterIds.Values.Length];
            }
        }


        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("{userId}/unequip-instance/{inventoryItemId}")]
        [Obsolete]
        public bool UnEquipItemInstance(string userId, Guid inventoryItemId)
        {
            return playerManager.UnequipItemInstance(AssertGetSessionToken(), userId, inventoryItemId);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("v2/{characterId}/unequip-instance/{inventoryItemId}")]
        public bool UnEquipItemInstance(Guid characterId, Guid inventoryItemId)
        {
            return playerManager.UnequipItemInstance(AssertGetSessionToken(), characterId, inventoryItemId);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("{userId}/unequipall")]
        [Obsolete]
        public bool UnequipAllItems(string userId)
        {
            return playerManager.UnequipAllItems(AssertGetSessionToken(), userId);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("v2/{characterId}/unequipall")]
        public bool UnequipAllItems(Guid characterId)
        {
            return playerManager.UnequipAllItems(AssertGetSessionToken(), characterId);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("{userId}/equip-instance/{inventoryItemId}")]
        [Obsolete]
        public bool EquipItemInstance(string userId, Guid inventoryItemId)
        {
            return playerManager.EquipItemInstance(AssertGetSessionToken(), userId, inventoryItemId);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("v2/{characterId}/equip-instance/{inventoryItemId}")]
        public bool EquipItemInstance(Guid characterId, Guid inventoryItemId)
        {
            return playerManager.EquipItemInstance(AssertGetSessionToken(), characterId, inventoryItemId);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("{userId}/equip/{item}")]
        [Obsolete]
        public bool EquipItem(string userId, Guid item)
        {
            return playerManager.EquipItem(AssertGetSessionToken(), userId, item);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("v2/{characterId}/equip/{item}")]
        public bool EquipItem(Guid characterId, Guid item)
        {
            return playerManager.EquipItem(AssertGetSessionToken(), characterId, item);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("{userId}/equipall")]
        [Obsolete]
        public bool EquipBestItems(string userId)
        {
            return playerManager.EquipBestItems(AssertGetSessionToken(), userId);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("v2/{characterId}/equipall")]
        public bool EquipBestItems(Guid characterId)
        {
            return playerManager.EquipBestItems(AssertGetSessionToken(), characterId);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("v2/{characterId}/gift/{receiverId}/{itemId}/{amount}")]
        public long GiftItemInstance(Guid characterId, Guid receiverId, Guid itemId, long amount)
        {
            return playerManager.GiftInventoryItem(AssertGetSessionToken(), characterId, receiverId, itemId, amount).Amount;
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("v3/{characterId}/gift/{receiverId}/{inventoryItemId}/{amount}")]
        public GiftItemResult GiftItem(Guid characterId, Guid receiverId, Guid inventoryItemId, long amount)
        {
            return playerManager.GiftInventoryItem(AssertGetSessionToken(), characterId, receiverId, inventoryItemId, amount);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("{characterId}/send/{alias}/{inventoryItemId}/{amount}")]
        public GiftItemResult GiftItem(Guid characterId, string alias, Guid inventoryItemId, long amount)
        {
            return playerManager.SendInventoryItem(AssertGetSessionToken(), characterId, alias, inventoryItemId, amount);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("{senderCharacterId}/send-coins/{receiverCharacterId}/{amount}")]
        public long SendCoins(Guid senderCharacterId, Guid receiverCharacterId, long amount)
        {
            return playerManager.SendCoins(AssertGetSessionToken(), senderCharacterId, receiverCharacterId, amount);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpGet("v2/{characterId}/vendor-instance/{item}/{amount}")]
        public long VendorItemInstance(Guid characterId, Guid item, long amount)
        {
            return playerManager.SellItemInstanceToVendor(AssertGetSessionToken(), characterId, item, amount);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("{userId}/appearance")]
        [Obsolete]
        public async Task<bool> UpdateSyntyAppearanceAsync(string userId, SyntyAppearance appearance)
        {
            userId = CleanupUserId(userId);
            var sessionId = HttpContext.GetSessionId();
            if (sessionInfoProvider.TryGet(sessionId, out var si) && si.ActiveCharacterId != null)
                return playerManager.UpdateAppearance(si.ActiveCharacterId.Value, appearance);
            return await UpdateSyntyAppearanceAsync(userId, "1", appearance);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("v2/{characterId}/appearance")]
        public async Task<bool> UpdateSyntyAppearanceAsync(Guid characterId, SyntyAppearance appearance)
        {
            var sessionId = HttpContext.GetSessionId();
            if (sessionInfoProvider.TryGet(sessionId, out var si) && si.ActiveCharacterId != null)
                return playerManager.UpdateAppearance(si.ActiveCharacterId.Value, appearance);
            return await UpdateSyntyAppearanceAsync(characterId, appearance);
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
                return playerManager.GetWebsitePlayers(si.UserId);
            }

            return new System.Collections.Generic.List<WebsitePlayer>();
        }
        private async Task<Player> GetPlayerInfoAsync()
        {
            var sessionId = HttpContext.GetSessionId();

            if (sessionInfoProvider.TryGet(sessionId, out var si) && si.ActiveCharacterId != null)
            {
                return Min(playerManager.GetPlayer(si.ActiveCharacterId.Value));
            }

            var twitchUser = await sessionInfoProvider.GetTwitchUserAsync(HttpContext.GetSessionId());
            if (twitchUser != null)
            {
                return Min(playerManager.GetPlayer(twitchUser.Id, "twitch", "1"));
            }

            var sessionToken = GetSessionToken();
            if (sessionToken == null || sessionToken.Expired || string.IsNullOrEmpty(sessionToken.AuthToken))
            {
                var auth = GetAuthToken();
                if (auth != null && !auth.Expired)
                {
                    return Min(playerManager.GetPlayer(auth.UserId, "1"));
                }

                return null;
            }

            return Min(playerManager.GetPlayer(sessionToken));
        }

        private Player Min(Player p)
        {
            p.InventoryItems = new List<RavenNest.Models.InventoryItem>();
            p.Statistics = null;
            p.State = null;
            p.Skills = null;
            p.Resources = null;
            return p;
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
                return playerManager.GetPlayer(twitchUser.Id, "twitch", "1");
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

using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using RavenNest.BusinessLogic;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Docs.Attributes;
using RavenNest.BusinessLogic.Game;
using RavenNest.Models;
using RavenNest.Sessions;
using RavenNest.Twitch;

namespace RavenNest.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [ApiDescriptor(Name = "Players API", Description = "Used for managing player data.")]
    public class PlayersController : ControllerBase
    {
        private readonly ISessionInfoProvider sessionInfoProvider;
        private readonly ISessionManager sessionManager;
        private readonly IPlayerManager playerManager;
        private readonly IRavenfallDbContextProvider dbProvider;
        private readonly IOptions<AppSettings> settings;
        private readonly ISecureHasher secureHasher;
        private readonly IAuthManager authManager;

        public PlayersController(
            ISessionInfoProvider sessionInfoProvider,
            ISessionManager sessionManager,
            IPlayerManager playerManager,
            IRavenfallDbContextProvider dbProvider,
            ISecureHasher secureHasher,
            IAuthManager authManager,
            IOptions<AppSettings> settings)
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
        [MethodDescriptor(Name = "Get Current Player", Description = "Gets the player data for the authenticated Twitch user, authenticated RavenNest user or current Game Session user.")]
        public Task<Player> Get()
        {
            return GetPlayerAsync();
        }

        [HttpGet("user")] // due to a misspelling in the customization tool. god darnit :P
        [MethodDescriptor(Name = "(Alias) Get Current Player", Description = "Gets the player data for the authenticated Twitch user, authenticated RavenNest user or current Game Session user.")]

        public Task<Player> GetUser()
        {
            return GetPlayerAsync();
        }

        //[HttpGet("fixsales")]
        //public async Task FixUserEquipmentsAsync()
        //{
        //    using (var db = dbProvider.Get())
        //    {
        //        //var userIdLock = Guid.Parse("CE4BB4EA-B435-4BE2-8CC1-F65CA44653A5");
        //        var characters = await db.Character
        //            .Where(x => x.UserIdLock == userIdLock)
        //            .Include(x => x.User)
        //            .Include(x => x.InventoryItem).ThenInclude(x => x.Item)
        //            .Include(x => x.Resources)
        //            .ToListAsync();

        //        //var sessionId = Guid.Parse("5703C6E3-E3DA-4E1E-BFED-2C36D4D1DD82");
        //        var gameEvents = await db.GameEvent.Where(x => x.GameSessionId == sessionId).ToListAsync();

        //        foreach (var gameEvent in gameEvents)
        //        {

        //            switch ((GameEventType)gameEvent.Type)
        //            {
        //                case GameEventType.ItemBuy:
        //                    {
        //                        var trade = JSON.Parse<ItemTradeUpdate>(gameEvent.Data);
        //                        var character = characters.FirstOrDefault(x => x.User.UserId == trade.BuyerId);
        //                        if (character != null)
        //                        {                                    
        //                            character.Resources.Coins -= trade.Cost;
        //                            db.Update(character.Resources);
        //                        }
        //                    }
        //                    break;

        //                case GameEventType.ItemSell:
        //                    {
        //                        var trade = JSON.Parse<ItemTradeUpdate>(gameEvent.Data);
        //                        var character = characters.FirstOrDefault(x => x.User.UserId == trade.SellerId);
        //                        if (character != null)
        //                        {
        //                            character.Resources.Coins += trade.Cost;
        //                            db.Update(character.Resources);
        //                        }
        //                    }
        //                    break;
        //            }

        //        }

        //        await db.SaveChangesAsync();
        //    }
        //}

        //[HttpGet("fixeq")]
        //public async Task FixUserEquipmentsAsync()
        //{
        //    using (var db = dbProvider.Get())
        //    {
        //        var characters = await db.Character
        //            .Include(x => x.InventoryItem).ThenInclude(x => x.Item)
        //            .Include(x => x.Appearance)
        //            .Include(x => x.Resources)
        //            .Include(x => x.Skills)
        //            .Include(x => x.Statistics)
        //            .ToListAsync();

        //        foreach (var character in characters)
        //        {
        //            PlayerManager.EquipBestItems(db, character);
        //        }
        //        await db.SaveChangesAsync();
        //    }
        //}

        [HttpPost("{userId}")]
        //[MethodDescriptor(Name = "Add Player to Game Session", Description = "Adds the target player to the ongoing session. This will lock the target player to the session and then return the player data.", RequiresSession = true)]
        public Task<Player> PlayerJoinAsync(string userId, Single<string> username)
        {
            return playerManager.AddPlayerAsync(AssertGetSessionToken(), userId, username.Value);
        }

        [HttpGet("{userId}")]
        [MethodDescriptor(
            Name = "Get Player by Twitch UserId",
            Description = "Get the target player using a Twitch UserId. This requires a session token for grabbing a local player but only an auth token for a global player.",
            RequiresAuth = true)
        ]
        public Task<Player> GetPlayerAsync(string userId)
        {
            if (GetSessionToken() == null)
            {
                AssertAuthTokenValidity(GetAuthToken());
                return playerManager.GetGlobalPlayerAsync(userId);
            }

            return playerManager.GetPlayerAsync(AssertGetSessionToken(), userId);
        }

        [HttpGet("{userId}/item/{item}")]
        //[MethodDescriptor(
        //    Name = "Add item to player",
        //    Description = "Adds an item to the target player, the item will automatically be equipped if it is better than any other existing equipped gear.",
        //    RequiresSession = true)
        //]
        public Task<AddItemResult> AddItemAsync(string userId, Guid item)
        {
            return playerManager.AddItemAsync(AssertGetSessionToken(), userId, item);
        }

        [HttpGet("{userId}/unequip/{item}")]
        //[MethodDescriptor(
        //    Name = "UnEquip item",
        //    Description = "UnEquips an item from the target player.",
        //    RequiresSession = true)
        //]
        public Task<bool> UnEquipItemAsync(string userId, Guid item)
        {
            return playerManager.UnEquipItemAsync(AssertGetSessionToken(), userId, item);
        }

        [HttpGet("{userId}/equip/{item}")]
        //[MethodDescriptor(
        //    Name = "Equip item",
        //    Description = "Equips an item from the target player.",
        //    RequiresSession = true)
        //]
        public Task<bool> EquipItemAsync(string userId, Guid item)
        {
            return playerManager.EquipItemAsync(AssertGetSessionToken(), userId, item);
        }

        //[HttpPost("appearance")]
        //[MethodDescriptor(
        //    Name = "Update player appearance as Twitch User",
        //    Description = "Update the target player with a new appearance. This requires you to be authenticated with Twitch to update.")
        //]
        //public async Task<bool> UpdateAppearanceForTwitchUserAsync(Many<int> appearance)
        //{
        //    var twitchUserSession = await sessionInfoProvider.GetTwitchUserAsync(HttpContext.Session);
        //    if (twitchUserSession == null)
        //    {
        //        return false;
        //    }

        //    return await playerManager.UpdateAppearanceAsync(twitchUserSession.Id, appearance.Values);
        //}

        //[HttpPost("{userId}/appearance")]
        //[MethodDescriptor(
        //    Name = "Update player appearance",
        //    Description = "Update the target player with a new appearance. This requires a session token to update a target player.",
        //    RequiresSession = true)
        //]
        //public Task<bool> UpdateAppearanceAsync(string userId, Many<int> appearance)
        //{
        //    var assertGetSessionToken = AssertGetSessionToken();
        //    return playerManager.UpdateAppearanceAsync(assertGetSessionToken, userId, appearance.Values);
        //}

        [HttpPost("{userId}/appearance")]
        //[MethodDescriptor(
        //    Name = "Update player appearance",
        //    Description = "Update the target player with a new appearance. This requires a session token to update a target player.",
        //    RequiresSession = true)]
        public Task<bool> UpdateSyntyAppearanceAsync(string userId, SyntyAppearance appearance)
        {
            return playerManager.UpdateSyntyAppearanceAsync(AssertGetSessionToken(), userId, appearance);
        }
        //UpdateSyntyAppearanceAsync


        [HttpPost("{userId}/experience")]
        //[MethodDescriptor(
        //    Name = "Update player experience",
        //    Description = "Update the target player with their current experience state.",
        //    RequiresSession = true)
        //]
        public Task<bool> UpdateExperienceAsync(string userId, Many<decimal> experience)
        {
            return playerManager.UpdateExperienceAsync(AssertGetSessionToken(), userId, experience.Values);
        }

        [HttpPost("{userId}/statistics")]
        //[MethodDescriptor(
        //    Name = "Update player statistics",
        //    Description = "Update the target player with their current statistics state, such as how many enemies killed, how many times they have died, etc.",
        //    RequiresSession = true)
        //]
        public Task<bool> UpdateStatisticsAsync(string userId, Many<decimal> statistics)
        {
            return playerManager.UpdateStatisticsAsync(AssertGetSessionToken(), userId, statistics.Values);
        }

        [HttpPost("{userId}/resources")]
        //[MethodDescriptor(
        //    Name = "Update player resources",
        //    Description = "Update the target player with their current resource state, such as coins, wood, ores, fish, wheat, etc.",
        //    RequiresSession = true)
        //]
        public Task<bool> UpdateResourcesAsync(string userId, Many<decimal> resources)
        {
            return playerManager.UpdateResourcesAsync(AssertGetSessionToken(), userId, resources.Values);
        }

        [HttpGet("{userId}/gift/{receiverUserId}/{itemId}")]
        //[MethodDescriptor(
        //    Name = "Gift an item to another player",
        //    Description = "Gift an item from one player to another, this will remove the item from the giver and add it to the receivers inventory. Gifted item will be equipped automatically if it is better than what is already equipped.",
        //    RequiresSession = true)
        //]
        public Task<bool> GiftItem(string userId, string receiverUserId, Guid itemId)
        {
            return playerManager.GiftItemAsync(AssertGetSessionToken(), userId, receiverUserId, itemId);
        }

        [HttpPost("update")]
        //[MethodDescriptor(
        //    Name = "Bulk player update",
        //    Description = "Update many players at the same time. This is used to save all currently playing players in one request.",
        //    RequiresSession = true)
        //]
        public Task<bool[]> UpdateMany(Many<PlayerState> states)
        {
            return playerManager.UpdateManyAsync(AssertGetSessionToken(), states.Values);
        }

        private AuthToken GetAuthToken()
        {
            if (HttpContext.Request.Headers.TryGetValue("auth-token", out var value))
            {
                return authManager.Get(value);
            }
            return null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AssertAuthTokenValidity(AuthToken authToken)
        {
            if (authToken == null) throw new NullReferenceException(nameof(authToken));
            if (authToken.UserId == Guid.Empty) throw new NullReferenceException(nameof(authToken.UserId));
            if (authToken.Expired) throw new SecurityTokenExpiredException("Session has expired.");
            if (string.IsNullOrEmpty(authToken.Token)) throw new SecurityTokenExpiredException("Session has expired.");
            if (authToken.Token != secureHasher.Get(authToken))
            {
                throw new SecurityTokenExpiredException("Session has expired.");
            }
        }
        private SessionToken AssertGetSessionToken()
        {
            var sessionToken = GetSessionToken();
            AssertSessionTokenValidity(sessionToken);
            return sessionToken;
        }

        private SessionToken GetSessionToken()
        {
            return HttpContext.Request.Headers.TryGetValue("session-token", out var value)
                ? sessionManager.Get(value)
                : null;
        }

        private async Task<Player> GetPlayerAsync()
        {
            var twitchUser = await sessionInfoProvider.GetTwitchUserAsync(HttpContext.Session);
            if (twitchUser != null)
            {
                return await playerManager.GetGlobalPlayerAsync(twitchUser.Id);
            }

            var sessionToken = GetSessionToken();
            if (sessionToken == null || sessionToken.Expired || string.IsNullOrEmpty(sessionToken.AuthToken))
            {
                var auth = GetAuthToken();
                if (auth != null && !auth.Expired)
                {
                    return await playerManager.GetGlobalPlayerAsync(auth.UserId);
                }

                return null;
            }

            return await playerManager.GetPlayerAsync(sessionToken);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void AssertSessionTokenValidity(SessionToken sessionToken)
        {
            if (sessionToken == null) throw new NullReferenceException(nameof(sessionToken));
            if (string.IsNullOrEmpty(sessionToken.AuthToken)) throw new NullReferenceException(nameof(sessionToken.AuthToken));
            if (sessionToken.Expired) throw new SecurityTokenExpiredException("Session has expired.");
        }

    }
}
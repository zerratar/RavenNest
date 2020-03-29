using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using RavenNest.BusinessLogic;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Docs.Attributes;
using RavenNest.BusinessLogic.Game;
using RavenNest.Models;
using RavenNest.Sessions;

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
        [MethodDescriptor(
            Name = "Get Current Player",
            Description = "Gets the player data for the authenticated Twitch user, authenticated RavenNest user or current Game Session user.")]
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

        [HttpPost("{userId}")]
        //[MethodDescriptor(Name = "Add Player to Game Session", Description = "Adds the target player to the ongoing session. This will lock the target player to the session and then return the player data.", RequiresSession = true)]
        public Player PlayerJoin(string userId, Single<string> username)
        {
            return playerManager.AddPlayer(AssertGetSessionToken(), userId, username.Value);
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
                return playerManager.GetPlayer(userId);
            }

            return playerManager.GetPlayer(AssertGetSessionToken(), userId);
        }

        [HttpGet("{userId}/craft/{item}")]
        public AddItemResult CraftItem(string userId, Guid item)
        {
            return playerManager.CraftItem(AssertGetSessionToken(), userId, item);
        }

        [HttpGet("{userId}/item/{item}")]
        public AddItemResult AddItem(string userId, Guid item)
        {
            return playerManager.AddItem(AssertGetSessionToken(), userId, item);
        }

        [HttpGet("{userId}/toggle-helmet")]
        public bool UnEquipItem(string userId)
        {
            return playerManager.ToggleHelmet(AssertGetSessionToken(), userId);
        }

        [HttpGet("{userId}/unequip/{item}")]
        public bool UnEquipItem(string userId, Guid item)
        {
            return playerManager.UnEquipItem(AssertGetSessionToken(), userId, item);
        }

        [HttpGet("{userId}/equip/{item}")]
        public bool EquipItem(string userId, Guid item)
        {
            return playerManager.EquipItem(AssertGetSessionToken(), userId, item);
        }

        [HttpGet("{userId}/gift/{receiverUserId}/{itemId}/{amount}")]
        public int GiftItem(string userId, string receiverUserId, Guid itemId, int amount)
        {
            return playerManager.GiftItem(AssertGetSessionToken(), userId, receiverUserId, itemId, amount);
        }

        [HttpGet("{userId}/vendor/{item}/{amount}")]
        public int VendorItem(string userId, Guid item, int amount)
        {
            return playerManager.VendorItem(AssertGetSessionToken(), userId, item, amount);
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
        public async Task<bool> UpdateSyntyAppearanceAsync(string userId, SyntyAppearance appearance)
        {
            var twitchUserSession = await sessionInfoProvider.GetTwitchUserAsync(HttpContext.Session);
            if (twitchUserSession != null)
            {
                return playerManager.UpdateSyntyAppearance(userId, appearance);
            }

            return playerManager.UpdateSyntyAppearance(AssertGetSessionToken(), userId, appearance);
        }

        [HttpPost("{userId}/experience")]
        public bool UpdateExperienceAsync(string userId, Many<decimal> experience)
        {
            return playerManager.UpdateExperience(AssertGetSessionToken(), userId, experience.Values);
        }

        [HttpPost("{userId}/statistics")]
        public bool UpdateStatistics(string userId, Many<decimal> statistics)
        {
            return playerManager.UpdateStatistics(AssertGetSessionToken(), userId, statistics.Values);
        }

        [HttpPost("{userId}/resources")]
        public bool UpdateResources(string userId, Many<decimal> resources)
        {
            return playerManager.UpdateResources(AssertGetSessionToken(), userId, resources.Values);
        }

        [HttpPost("update")]
        public bool[] UpdateMany(Many<PlayerState> states)
        {
            return playerManager.UpdateMany(AssertGetSessionToken(), states.Values);
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
                return playerManager.GetPlayer(twitchUser.Id.ToString());
            }

            var sessionToken = GetSessionToken();
            if (sessionToken == null || sessionToken.Expired || string.IsNullOrEmpty(sessionToken.AuthToken))
            {
                var auth = GetAuthToken();
                if (auth != null && !auth.Expired)
                {
                    return playerManager.GetGlobalPlayer(auth.UserId);
                }

                return null;
            }

            return playerManager.GetPlayer(sessionToken);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void AssertSessionTokenValidity(SessionToken sessionToken)
        {
            if (sessionToken == null) throw new NullReferenceException(nameof(sessionToken));
            if (string.IsNullOrEmpty(sessionToken.AuthToken)) throw new NullReferenceException(nameof(sessionToken.AuthToken));
            if (sessionToken.Expired) throw new Exception("Session has expired.");
        }

    }
}
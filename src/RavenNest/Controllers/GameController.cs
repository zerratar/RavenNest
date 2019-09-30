using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using RavenNest.BusinessLogic;
using RavenNest.BusinessLogic.Docs.Attributes;
using RavenNest.BusinessLogic.Game;
using RavenNest.Models;

namespace RavenNest.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    //[ApiDescriptor(Name = "Game API", Description = "Used for handling game sessions and polling game events.")]
    public class GameController : ControllerBase
    {
        private readonly IAuthManager authManager;
        private readonly ISessionManager sessionManager;
        private readonly IGameManager gameManager;
        private readonly ISecureHasher secureHasher;

        public GameController(
            IAuthManager authManager,
            ISessionManager sessionManager,
            IGameManager gameManager,
            ISecureHasher secureHasher)
        {
            this.authManager = authManager;
            this.sessionManager = sessionManager;
            this.gameManager = gameManager;
            this.secureHasher = secureHasher;
        }

        [HttpGet]
        //[MethodDescriptor(
        //    Name = "Get info about current game session",
        //    Description = "This will return information about the ongoing game session such as uptime, peak player count and more.",
        //    RequiresSession = true,
        //    RequiresAuth = false,
        //    RequiresAdmin = false)
        //]
        public Task<GameInfo> GetAsync()
        {
            var session = GetSessionToken();
            AssertSessionTokenValidity(session);
            return gameManager.GetGameInfoAsync(session);
        }

        [HttpPost("{clientVersion}/{accessKey}")]
        //[MethodDescriptor(
        //    Name = "Start a game session",
        //    Description = "Start a new or continue on an existing non-stopped game session. This will also return a refreshed session token, required for updating any player, marketplace or game info.",
        //    RequiresSession = true,
        //    RequiresAuth = false,
        //    RequiresAdmin = false)
        //]
        public Task<SessionToken> BeginSessionAsync(string clientVersion, string accessKey, Single<bool> local)
        {
            var authToken = GetAuthToken();
            AssertAuthTokenValidity(authToken);

            var session = this.sessionManager.BeginSessionAsync(authToken, clientVersion, accessKey, local.Value);
            if (session == null)
            {
                HttpContext.Response.StatusCode = 403;
                return null;
            }

            return session;
        }

        [HttpDelete("raid/{username}")]
        //[MethodDescriptor(
        //    Name = "Raid another streamer",
        //    Description = "When you're done with your stream, don't forget to raid someone! This will end your current game session and bring all your current playing players into the target Twitch user's stream playing Ravenfall.",
        //    RequiresSession = true,
        //    RequiresAuth = false,
        //    RequiresAdmin = false)
        //]
        public Task<bool> EndSessionAndRaidAsync(string username, Single<bool> war)
        {
            var session = GetSessionToken();
            AssertSessionTokenValidity(session);
            return this.sessionManager.EndSessionAndRaidAsync(session, username, war.Value);
        }

        [HttpDelete]
        //[MethodDescriptor(
        //    Name = "End the session",
        //    Description = "This will end your current game session. This should be called whenever the game stops.",
        //    RequiresSession = true,
        //    RequiresAuth = false,
        //    RequiresAdmin = false)
        //]
        public Task EndSessionAsync()
        {
            var session = GetSessionToken();
            AssertSessionTokenValidity(session);
            return this.sessionManager.EndSessionAsync(session);
        }

        //[HttpGet("events/{revision}")]
        ////[MethodDescriptor(
        ////    Name = "Poll game events",
        ////    Description = "Poll the latest game events after a specific revision. This will hold your request up to 20 seconds or until a new game event has been added.",
        ////    RequiresSession = true,
        ////    RequiresAuth = false,
        ////    RequiresAdmin = false)
        ////]
        //public async Task<EventCollection> PollEventsAsync(int revision)
        //{
        //    var totalWait = 0;
        //    var sessionToken = GetSessionToken();
        //    AssertSessionTokenValidity(sessionToken);
        //    while (totalWait < 20_000)
        //    {
        //        try
        //        {
        //            var events = await gameManager.GetGameEventsAsync(sessionToken);
        //            if (events.Count > 0)
        //            {
        //                return events;
        //            }

        //            await Task.Delay(250);
        //            totalWait += 250;
        //        }
        //        catch
        //        {
        //            return new EventCollection();
        //        }
        //    }
        //    return new EventCollection();
        //}

        private SessionToken GetSessionToken()
        {
            if (HttpContext.Request.Headers.TryGetValue("session-token", out var value))
            {
                return sessionManager.Get(value);
            }
            return null;
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void AssertSessionTokenValidity(SessionToken sessionToken)
        {
            if (sessionToken == null) throw new NullReferenceException(nameof(sessionToken));
            if (sessionToken.SessionId == Guid.Empty) throw new NullReferenceException(nameof(sessionToken.SessionId));
            if (sessionToken.Expired) throw new SecurityTokenExpiredException("Session has expired.");
        }
    }

}
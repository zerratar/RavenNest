﻿using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using RavenNest.BusinessLogic;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Game;
using RavenNest.Models;

namespace RavenNest.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    //[ApiDescriptor(Name = "Game API", Description = "Used for handling game sessions and polling game events.")]
    public class SessionController : ControllerBase
    {
        private readonly IGameData gameData;
        private readonly IAuthManager authManager;
        private readonly ISessionManager sessionManager;
        private readonly IGameManager gameManager;
        private readonly ISecureHasher secureHasher;

        public SessionController(
            IGameData gameData,
            IAuthManager authManager,
            ISessionManager sessionManager,
            IGameManager gameManager,
            ISecureHasher secureHasher)
        {
            this.gameData = gameData;
            this.authManager = authManager;
            this.sessionManager = sessionManager;
            this.gameManager = gameManager;
            this.secureHasher = secureHasher;
        }

        [HttpGet]
        public GameInfo Get()
        {
            var session = GetSessionToken();
            AssertSessionTokenValidity(session);
            return gameManager.GetGameInfo(session);
        }

        [HttpPost("{clientVersion}/{accessKey}")]
        public async Task<SessionToken> BeginSessionAsync(string clientVersion, string accessKey, Two<bool, float> param)
        {
            var authToken = GetAuthToken();
            AssertAuthTokenValidity(authToken);

            var session = await this.sessionManager.BeginSessionAsync(
                authToken,
                clientVersion,
                accessKey,
                param.Value1,
                param.Value2);

            if (session == null)
            {
                HttpContext.Response.StatusCode = 403;
                return null;
            }

            return session;
        }

        [HttpDelete("raid/{username}")]
        public bool EndSessionAndRaid(string username, Single<bool> war)
        {
            var session = GetSessionToken();
            AssertSessionTokenValidity(session);
            return this.sessionManager.EndSessionAndRaid(session, username, war.Value);
        }

        [HttpDelete]
        public void EndSession()
        {
            var session = GetSessionToken();
            AssertSessionTokenValidity(session);
            this.sessionManager.EndSession(session);
        }

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
            if (authToken.Expired) throw new Exception("Session has expired.");
            if (string.IsNullOrEmpty(authToken.Token)) throw new Exception("Session has expired.");
            if (authToken.Token != secureHasher.Get(authToken))
            {
                throw new Exception("Session has expired.");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void AssertSessionTokenValidity(SessionToken sessionToken)
        {
            if (sessionToken == null) throw new NullReferenceException(nameof(sessionToken));
            if (sessionToken.SessionId == Guid.Empty) throw new NullReferenceException(nameof(sessionToken.SessionId));
            if (sessionToken.Expired) throw new Exception("Session has expired.");
        }
    }

}
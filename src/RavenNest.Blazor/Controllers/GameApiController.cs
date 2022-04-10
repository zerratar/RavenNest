using System;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Game;
using RavenNest.Models;
using RavenNest.Sessions;

namespace RavenNest.Controllers
{
    public class GameApiController : ControllerBase
    {
        protected string SessionId => HttpContext.GetSessionId();
        private readonly IGameData gameData;
        private readonly IAuthManager authManager;
        private readonly ISessionInfoProvider sessionInfoProvider;
        private readonly ISessionManager sessionManager;
        private readonly ISecureHasher secureHasher;
        private readonly ILogger logger;

        public GameApiController(
            ILogger logger,
            IGameData gameData,
            IAuthManager authManager,
            ISessionInfoProvider sessionInfoProvider,
            ISessionManager sessionManager,
            ISecureHasher secureHasher)
        {
            this.logger = logger;
            this.gameData = gameData;
            this.authManager = authManager;
            this.sessionInfoProvider = sessionInfoProvider;
            this.sessionManager = sessionManager;
            this.secureHasher = secureHasher;
        }

        protected SessionToken GetSessionToken()
        {
            if (HttpContext.Request.Headers.TryGetValue("session-token", out var value))
            {
                return sessionManager.Get(value);
            }
            return null;
        }

        protected AuthToken GetAuthToken()
        {
            if (HttpContext.Request.Headers.TryGetValue("auth-token", out var value))
                return authManager.Get(value);
            if (sessionInfoProvider.TryGetAuthToken(SessionId, out var authToken))
                return authToken;
            return null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected DataModels.User GetCurrentUser()
        {
            var authToken = GetAuthToken();
            AssertAuthTokenValidity(authToken);
            return gameData.GetUser(authToken.UserId);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void AssertAdminAuthToken(AuthToken authToken)
        {
            var user = gameData.GetUser(authToken.UserId);
            if (!user.IsAdmin.GetValueOrDefault())
                throw new Exception("You do not have permissions to call this API");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void AssertAuthTokenValidity(AuthToken authToken, [CallerMemberName] string callingMethod = null)
        {
            string errorMessage = null;
            if (authToken == null || string.IsNullOrEmpty(authToken.Token))
            {
                errorMessage = "Auth token cannot be null.";
            }
            else if (authToken.UserId == Guid.Empty)
            {
                errorMessage = "UserId cannot be null.";
            }
            else if (authToken.Expired)
            {
                errorMessage = "Auth token has expired.";
            }
            else if (authToken.Token != secureHasher.Get(authToken))
            {
                errorMessage = "Auth token did not match expected value.";
            }

            if (!string.IsNullOrEmpty(errorMessage))
            {
                var authTokenJson = authToken != null ? Newtonsoft.Json.JsonConvert.SerializeObject(authToken) : "{}";
                logger.LogError(callingMethod + "->AssertAuthTokenValidity failed on request with error: " + errorMessage + ". " + authTokenJson);
                throw new Exception(errorMessage);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected string GetHeaderValues()
        {
            var headers = HttpContext?.Request?.Headers;
            if (headers != null)
            {
                return string.Join(", ", headers.Select(header => header.Key + ": " + header.Value));
            }

            return null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void AssertSessionTokenValidity(SessionToken sessionToken, [CallerMemberName] string callingMethod = null)
        {
            string errorMessage = null;
            if (sessionToken == null)
            {
                errorMessage = "Session token cannot be null. ";
            }
            else if (sessionToken.SessionId == Guid.Empty)
            {
                errorMessage = "Session Id cannot be null.";
            }
            else if (sessionToken.Expired)
            {
                errorMessage = "Session token has expired.";
            }
#if DEBUG
            if (!string.IsNullOrEmpty(errorMessage))
            {
                errorMessage += " " + GetHeaderValues();
            }
#endif

            if (!string.IsNullOrEmpty(errorMessage))
            {
                var authTokenJson = sessionToken != null ? Newtonsoft.Json.JsonConvert.SerializeObject(sessionToken) : "{}";
                logger.LogError(callingMethod + "->AssertSessionTokenValidity failed on request with error: " + errorMessage + ". " + authTokenJson);
                throw new Exception(errorMessage);
            }
        }
    }
}

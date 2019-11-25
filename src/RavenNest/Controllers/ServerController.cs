using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using RavenNest.BusinessLogic.Game;
using RavenNest.Models;

namespace RavenNest.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ServerController : ControllerBase
    {
        private readonly IAuthManager authManager;
        private readonly IServerManager serverManager;
        private readonly ISecureHasher secureHasher;

        public ServerController(
            IAuthManager authManager,
            IServerManager serverManager,
            ISecureHasher secureHasher)
        {
            this.authManager = authManager;
            this.serverManager = serverManager;
            this.secureHasher = secureHasher;
        }

        [HttpPost("message")]
        public async Task BroadcastMessageAsync([FromBody]string message)
        {
            var authToken = GetAuthToken();
            AssertAuthTokenValidity(authToken);

            if (!authManager.IsAdmin(authToken))
            {
                return;
            }

            await serverManager.BroadcastMessageAsync(message);
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
    }

}
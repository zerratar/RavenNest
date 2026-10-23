using System;
using Microsoft.AspNetCore.Mvc;
using RavenNest.BusinessLogic.Game;
using RavenNest.Models;

namespace RavenNest.Controllers
{
    /// <summary>
    /// The game client's side of signing in through the website, and of signing in again on its
    /// own with a device token. See <see cref="ClientLoginManager"/>.
    /// </summary>
    [Route("api/auth/device")]
    [ApiController]
    public class ClientLoginController : ControllerBase
    {
        private readonly ClientLoginManager clientLogin;
        private readonly IAuthManager authManager;
        private readonly ISecureHasher secureHasher;

        public ClientLoginController(ClientLoginManager clientLogin, IAuthManager authManager, ISecureHasher secureHasher)
        {
            this.clientLogin = clientLogin;
            this.authManager = authManager;
            this.secureHasher = secureHasher;
        }

        public class StartModel
        {
            public string Secret { get; set; }
            public string DeviceName { get; set; }
        }

        public class StartResult
        {
            public string Code { get; set; }
            /// <summary>The code as both screens show it, "ABCD-EFGH".</summary>
            public string DisplayCode { get; set; }
            /// <summary>The website page to open, relative to the site: "/login/device/ABCDEFGH".</summary>
            public string Path { get; set; }
            public int ExpiresInSeconds { get; set; }
        }

        public class PollModel
        {
            public string Code { get; set; }
            public string Secret { get; set; }
        }

        public class PollResult
        {
            /// <summary>pending, approved, denied, expired or unknown.</summary>
            public string Status { get; set; }
            public AuthToken AuthToken { get; set; }
            public string DeviceToken { get; set; }
            public string UserName { get; set; }
        }

        public class DeviceModel
        {
            public string DeviceToken { get; set; }
            public string DeviceName { get; set; }
        }

        public class DeviceLoginResult
        {
            public AuthToken AuthToken { get; set; }
            public string UserName { get; set; }
        }

        /// <summary>Starts a sign in through the website for the client that holds the secret.</summary>
        [HttpPost("start")]
        public ActionResult<StartResult> Start(StartModel model)
        {
            var code = clientLogin.Start(model?.Secret, model?.DeviceName);
            if (code == null)
            {
                return BadRequest();
            }

            return new StartResult
            {
                Code = code,
                DisplayCode = ClientLoginManager.FormatCode(code),
                Path = "/login/device/" + code,
                ExpiresInSeconds = (int)ClientLoginManager.RequestLifetime.TotalSeconds,
            };
        }

        /// <summary>Asks whether the sign in was approved. Approved hands out the tokens, once.</summary>
        [HttpPost("poll")]
        public ActionResult<PollResult> Poll(PollModel model)
        {
            var result = clientLogin.Poll(model?.Code, model?.Secret);
            return new PollResult
            {
                Status = result.Status.ToString().ToLowerInvariant(),
                AuthToken = result.AuthToken,
                DeviceToken = result.DeviceToken,
                UserName = result.UserName,
            };
        }

        /// <summary>Signs in with a device token. 401 when it is not valid any more.</summary>
        [HttpPost("login")]
        public ActionResult<DeviceLoginResult> Login(DeviceModel model)
        {
            var result = clientLogin.Login(model?.DeviceToken);
            if (result == null)
            {
                return Unauthorized();
            }

            return new DeviceLoginResult { AuthToken = result.AuthToken, UserName = result.UserName };
        }

        /// <summary>A device token for a client signed in with a password, for remember me.</summary>
        [HttpPost("register")]
        public ActionResult Register(DeviceModel model)
        {
            var authToken = GetAuthToken();
            if (!IsValid(authToken))
            {
                return Unauthorized();
            }

            var token = clientLogin.Register(authToken, model?.DeviceName);
            if (token == null)
            {
                return Unauthorized();
            }

            // as JSON: a plain string result goes out as text/plain, which the game cannot read as its string,
            // so "remember this computer" never stored the token
            return new JsonResult(token);
        }

        /// <summary>Forgets the device, when the game logs out.</summary>
        [HttpPost("revoke")]
        public ActionResult Revoke(DeviceModel model)
        {
            clientLogin.Revoke(model?.DeviceToken);
            return Ok();
        }

        private AuthToken GetAuthToken()
        {
            return HttpContext.Request.Headers.TryGetValue("auth-token", out var value) ? authManager.Get(value) : null;
        }

        private bool IsValid(AuthToken token)
        {
            return token != null
                && !string.IsNullOrEmpty(token.Token)
                && token.UserId != Guid.Empty
                && !token.Expired
                && secureHasher.IsValid(token);
        }
    }
}

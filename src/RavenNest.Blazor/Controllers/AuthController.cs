using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using RavenNest.BusinessLogic.Docs.Attributes;
using RavenNest.BusinessLogic.Game;
using RavenNest.Models;
using RavenNest.Sessions;

namespace RavenNest.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [ApiDescriptor(Name = "Authentication API", Description = "Used for authenticating with the RavenNest API.")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthManager authManager;
        private readonly ISessionInfoProvider sessionInfoProvider;

        public AuthController(
            IAuthManager authManager,
            ISessionInfoProvider sessionInfoProvider)
        {
            this.authManager = authManager;
            this.sessionInfoProvider = sessionInfoProvider;
        }

        [HttpGet]
        [MethodDescriptor(
            Name = "Check current authentication state",
            Description = "Doing a GET to this api will return whether or not you are logged in and can use the RavenNest API.",
            ResponseExample = "\"You are logged in\"",
            RequiresSession = false,
            RequiresAuth = false,
            RequiresAdmin = false)
        ]
        public string Get()
        {
            var token = GetAuthToken();
            if (token == null)
            {
                return "Not logged in";
            }

            return "You are logged in";
        }

        [HttpPost]
        [MethodDescriptor(
            Name = "Authenticate",
            Description = "Authenticate to RavenNest API and retrieve an auth token. The auth token is required for many of the available APIs. This method be called every hour or so to keep your auth token valid.",
            RequiresSession = false,
            RequiresAuth = false,
            RequiresAdmin = false)
        ]
        public AuthToken AuthenticateAsync(AuthModel model)
        {
            return this.authManager.Authenticate(model.Username, model.Password);
        }

        [HttpPost("login")]
        [MethodDescriptor(
            Name = "Login",
            Description = "Authenticate with RavenNest Website using a username/password combination",
            RequiresSession = false,
            RequiresAuth = false,
            RequiresAdmin = false)
        ]
        public Task<SessionInfo> LoginAsync(AuthModel model)
        {
            var authenticateAsync = this.authManager.Authenticate(model.Username, model.Password);
            return sessionInfoProvider.SetAuthTokenAsync(SessionId, authenticateAsync);
        }

        [HttpGet("logout")]
        [MethodDescriptor(
            Name = "Logout",
            Description = "Clears the current logged in website session",
            RequiresSession = false,
            RequiresAuth = false,
            RequiresAdmin = false)
        ]
        public SessionInfo Logout()
        {
            sessionInfoProvider.Clear(SessionId);
            return new SessionInfo();
        }

        [HttpPost("signup")]
        [MethodDescriptor(
            Name = "Signup",
            Description = "First time user setup requires to assign a password",
            RequiresSession = false,
            RequiresAuth = false,
            RequiresAdmin = false,
            RequiresTwitchAuth = true)
        ]
        public async Task<SessionInfo> SignUpAsync(PasswordModel password)
        {
            var user = await sessionInfoProvider.GetTwitchUserAsync(SessionId);
            authManager.SignUp(user.Id, user.Login, user.DisplayName, user.Email, password.Password);
            return await sessionInfoProvider.StoreAsync(SessionId);
        }

        private string SessionId => HttpContext.GetSessionId();

        private AuthToken GetAuthToken()
        {
            if (HttpContext.Request.Headers.TryGetValue("auth-token", out var value))
            {
                return authManager.Get(value);
            }
            return null;
        }
        public class AuthModel
        {
            public string Username { get; set; }
            public string Password { get; set; }
        }

        public class PasswordModel
        {
            public string Password { get; set; }
        }
    }
}

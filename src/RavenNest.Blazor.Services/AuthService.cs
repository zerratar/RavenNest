using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using RavenNest.Blazor.Services.Models;
using RavenNest.BusinessLogic;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Extensions;
using RavenNest.BusinessLogic.Game;
using RavenNest.Kick;
using RavenNest.Models;
using RavenNest.Sessions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace RavenNest.Blazor.Services
{
    public class AuthService : RavenNestService
    {
        private readonly ILogger<AuthService> logger;
        private readonly IRavenBotApiClient ravenbotApi;
        private readonly GameData gameData;
        private readonly IAuthManager authManager;
        private readonly PlayerManager playerManager;
        private readonly LogoService logoService;
        private readonly AppSettings settings;

        public AuthService(
            IOptions<AppSettings> settings,
            ILogger<AuthService> logger,
            IRavenBotApiClient ravenbotApi,
            GameData gameData,
            IAuthManager authManager,
            PlayerManager playerManager,
            IHttpContextAccessor accessor,
            SessionInfoProvider sessionInfoProvider,
            LogoService logoService)
            : base(accessor, sessionInfoProvider)
        {
            this.logger = logger;
            this.ravenbotApi = ravenbotApi;
            this.gameData = gameData;
            this.authManager = authManager;
            this.playerManager = playerManager;
            this.logoService = logoService;
            this.settings = settings.Value;
        }

        public void Logout()
        {
            var session = Context.GetSessionId();
            sessionInfoProvider.Clear(session);
        }

        public bool HasActiveGameSession()
        {
            return GetActiveGameSession() != null;
        }

        public DataModels.GameSession GetActiveGameSession()
        {
            var session = GetSession();
            return gameData.GetActiveSessions().FirstOrDefault(x => x.UserId == session.UserId);
        }

        public async Task GrantPubSubAccessAsync(string accessToken)
        {
            var session = Context.GetSessionId();
            var result = await sessionInfoProvider.SetTwitchTokenAsync(session, accessToken);
            var user = await sessionInfoProvider.GetTwitchUserAsync(session, accessToken);
            if (user != null)
            {
                await playerManager.CreatePlayerIfNotExists(user.Id, "twitch", user.Login, "1");
                var u = gameData.GetUserByTwitchId(user.Id);
                if (u != null)
                {
                    if (u.Status >= 1)
                    {
                        return;
                    }

                    gameData.SetUserProperty(u.Id, UserProperties.Twitch_PubSub, accessToken);
                    //await ravenbotApi.SendTwitchPubSubAccessTokenAsync(user.Id, user.Login, accessToken);
                    await logoService.UpdateUserLogosAsync(user);
                    await ravenbotApi.UpdateUserSettingsAsync(u.Id);
                }
            }
        }

        public async Task<SessionInfo> KickLoginAsync(string code, string scope, string code_verifier, string code_challenge, string origin = null)
        {
            try
            {
                var session = Context.GetSessionId();

                // we need to get the access token to use

                // Must match the redirect_uri sent to the authorize endpoint exactly, or Kick
                // rejects the token exchange. Same origin resolution as GetKickLoginUrl.
                var redirectUrl = ResolveOrigin(origin) + "/login/kick";
                var kick = new KickRequests(code, scope, code_verifier, code_challenge, redirectUrl, settings.KickClientId, settings.KickClientSecret);
                var kickAuth = await kick.AuthenticateAsync();

                var result = await sessionInfoProvider.SetKickTokenAsync(session, kickAuth.access_token);
                var sessionInfo = result.SessionInfo;
                var user = await sessionInfoProvider.GetKickUserAsync(session, kickAuth.access_token);
                if (user != null)
                {
                    await playerManager.CreatePlayerIfNotExists(user.Id.ToString(), "kick", user.Name, "1");
                    var u = gameData.GetUserByKickId(user.Id.ToString());
                    if (u != null)
                    {
                        // store token that has access to reading channel point reward redeems?
                        // so we can tell the chat bot to use that when listening for rewards.

                        // u.Token = accessToken;
                        if (u.Status >= 1)
                        {
                            sessionInfo.Authenticated = false;
                            sessionInfo.AuthToken = null;
                            return sessionInfo;
                        }

                        // Same single path as the Twitch branch above, so both platforms leave the
                        // account in the same state.
                        var kickNameChanged = UserNameSync.Apply(gameData, u, "kick", user.Id.ToString(), user.Name, user.Name);
                        if (kickNameChanged)
                        {
                            sessionInfo.UserName = u.UserName;
                            sessionInfo.UserNameChanged = true;
                        }

                        var userAccess = gameData.GetUserAccess(u.Id, "kick");
                        if (userAccess != null)
                        {
                            userAccess.AccessToken = kickAuth.access_token;
                            userAccess.Updated = System.DateTime.UtcNow;
                        }

                        sessionInfo.Patreon = ModelMapper.Map(gameData.GetPatreonUser(u.Id));

                        gameData.SetUserProperty(u.Id, UserProperties.Kick_AccessToken, kickAuth.access_token);

                        await ravenbotApi.UpdateUserSettingsAsync(u.Id);

                        // Only on an actual rename. Moves a live session to the renamed channel
                        // instead of leaving the bot in the old one until the next game restart.
                        if (kickNameChanged)
                        {
                            await ravenbotApi.PushUserNamesAsync(u.Id);
                        }

                        //await ravenbotApi.SendTwitchPubSubAccessTokenAsync(user.Id, user.Login, accessToken);
                    }

                    //await logoService.UpdateUserLogosAsync(user);
                }

                return sessionInfo;
            }
            catch
            {
                return null;
            }
        }

        public async Task<SessionInfo> TwitchLoginAsync(string accessToken)
        {
            try
            {
                var session = Context.GetSessionId();
                var result = await sessionInfoProvider.SetTwitchTokenAsync(session, accessToken);
                var sessionInfo = result.SessionInfo;
                var user = await sessionInfoProvider.GetTwitchUserAsync(session, accessToken);
                if (user != null)
                {
                    await playerManager.CreatePlayerIfNotExists(user.Id, "twitch", user.Login, "1");
                    var u = gameData.GetUserByTwitchId(user.Id);
                    if (u != null)
                    {
                        // store token that has access to reading channel point reward redeems?
                        // so we can tell the chat bot to use that when listening for rewards.

                        // u.Token = accessToken;
                        if (u.Status >= 1)
                        {
                            sessionInfo.Authenticated = false;
                            sessionInfo.AuthToken = null;
                            return sessionInfo;
                        }

                        // One call keeps UserName, DisplayName, the access row and the character
                        // names in step. This used to update only some of them here and a different
                        // subset elsewhere, which is how accounts ended up half renamed.
                        var twitchNameChanged = UserNameSync.Apply(gameData, u, "twitch", user.Id, user.Login, user.DisplayName);
                        if (twitchNameChanged)
                        {
                            sessionInfo.UserName = u.UserName;
                            sessionInfo.UserNameChanged = true;
                        }

                        var twitchUserAccess = gameData.GetUserAccess(u.Id, "twitch");
                        if (twitchUserAccess != null)
                        {
                            twitchUserAccess.AccessToken = accessToken;
                            twitchUserAccess.Updated = System.DateTime.UtcNow;
                        }

                        sessionInfo.Patreon = ModelMapper.Map(gameData.GetPatreonUser(u.Id));

                        gameData.SetUserProperty(u.Id, UserProperties.Twitch_PubSub, accessToken);

                        await ravenbotApi.UpdateUserSettingsAsync(u.Id);

                        // Only on an actual rename. Moves a live session to the renamed channel
                        // instead of leaving the bot in the old one until the next game restart.
                        if (twitchNameChanged)
                        {
                            await ravenbotApi.PushUserNamesAsync(u.Id);
                        }

                        //await ravenbotApi.SendTwitchPubSubAccessTokenAsync(user.Id, user.Login, accessToken);
                    }

                    await logoService.UpdateUserLogosAsync(user);
                }

                return sessionInfo;
            }
            catch
            {
                return null;
            }
        }

        // The platform suffix is now handled by UserNameSync.GetPlatformSuffix, which keeps the
        // suffix instead of rebuilding the name with string replacement. The old approach stripped
        // every occurrence of the name, so an account like "abc@abcstream" lost the wrong part.

        public async Task<SessionInfo> LoginAsync(UserLoginModel model)
        {
            var id = SessionCookie.GetSessionId(Context);
            var auth = authManager.Authenticate(model.Username, model.Password);
            if (auth == null)
            {
                logger.LogError("Login for " + model.Username + " failed. " + nameof(IAuthManager.Authenticate) + " returned null.");
                return new SessionInfo { };
            }

            var user = gameData.GetUser(auth.UserId);
            if (user != null && user.Status >= 1)
            {
                return new SessionInfo() { Authenticated = false };
            }

            var result = await sessionInfoProvider.SetAuthTokenAsync(id, auth);
            if (result == null)
            {
                logger.LogError("Login for " + model.Username + " failed. " + nameof(SessionInfoProvider.SetAuthTokenAsync) + " returned null.");
                return new SessionInfo { };
            }

            result.SessionInfo.Patreon = ModelMapper.Map(gameData.GetPatreonUser(user.Id));
            return result.SessionInfo;
        }


        /// <summary>
        /// Works out the origin ("scheme://host:port") that an OAuth provider should send the user
        /// back to.
        /// </summary>
        /// <remarks>
        /// This is the reason signing in on localhost ended up at www.ravenfall.stream. These URLs
        /// are built from a button click, which in Blazor Server runs on the SignalR circuit rather
        /// than during an HTTP request, and <see cref="RavenNestService.Context"/> is null there.
        /// The host lookup silently fell through to the production fallback every time.
        ///
        /// <para>
        /// Callers running in a component pass NavigationManager.BaseUri, which is correct on the
        /// circuit. HttpContext is still used when there is one, and the production host remains
        /// the last resort.
        /// </para>
        ///
        /// <para>
        /// The scheme is taken from the request rather than hardcoded to https, because Twitch and
        /// Kick both allow http for localhost and that is exactly the case this needs to support.
        /// </para>
        /// </remarks>
        private string ResolveOrigin(string origin)
        {
            if (!string.IsNullOrEmpty(origin) &&
                Uri.TryCreate(origin, UriKind.Absolute, out var parsed))
            {
                return parsed.GetLeftPart(UriPartial.Authority);
            }

            var request = Context?.Request;
            if (request != null && request.Host.HasValue)
            {
                return request.Scheme + "://" + request.Host.Value;
            }

            return "https://www.ravenfall.stream";
        }

        public string GetTwitchLoginUrl(string redirectToAfterLogin = "", string origin = null)
        {
            //could move List to parameters for passing more parameters for twitch to give back. This is an odd way of doing it but bonus effect
            //of adding some protection against CSRF
            List<RavenNest.Models.StateParameters> StateParametersList = new();
            if (!string.IsNullOrEmpty(redirectToAfterLogin))
                StateParametersList.Add(new("redirect", redirectToAfterLogin));

            var baseUrl = ResolveOrigin(origin);

            return $"https://id.twitch.tv/oauth2/authorize?client_id={settings.TwitchClientId}&redirect_uri="
                    + $"{baseUrl}/login/twitch"
                    + "&response_type=token&scope=user:read:email+bits:read+channel:read:subscriptions+channel:read:redemptions"
                    + "&state=" + GetRandomizedBase64EncodedStateParameters(StateParametersList);
        }

        public string GetKickLoginUrl(string redirectToAfterLogin = "", string origin = null)
        {
            // Generate a new code verifier and code challenge for this login attempt.
            var codeVerifier = PkceUtil.GenerateCodeVerifier();
            var base64EncodedCodeChallenge = PkceUtil.GenerateCodeChallenge(codeVerifier);

            // (Store codeVerifier securely in the session or state to use later for token exchange)
            var scope = "user:read+channel:read+events:subscribe";
            List<StateParameters> StateParametersList = new();
            if (!string.IsNullOrEmpty(redirectToAfterLogin))
            {
                StateParametersList.Add(new("redirect", redirectToAfterLogin));
            }

            StateParametersList.Add(new("code_verifier", codeVerifier));
            StateParametersList.Add(new("code_challenge", base64EncodedCodeChallenge));
            StateParametersList.Add(new("scope", scope));

            var baseUrl = ResolveOrigin(origin);
            var kickUrl = $"https://id.kick.com/oauth/authorize?client_id={settings.KickClientId}&redirect_uri="
                    + $"{baseUrl}/login/kick"
                    + "&response_type=code"
                    + "&scope=" + scope
                    + "&code_challenge=" + base64EncodedCodeChallenge
                    + "&code_challenge_method=S256"
                    + "&state=" + GetRandomizedBase64EncodedStateParameters(StateParametersList);

            return kickUrl;
        }


        //Create a 64BaseEncodedString of a JSON Object for Twitch to return back to us
        public string GetRandomizedBase64EncodedStateParameters(List<RavenNest.Models.StateParameters> stateParameters)
        {
            return authManager.GetRandomizedBase64EncodedStateParameters(stateParameters);
        }


        public List<RavenNest.Models.StateParameters> GetDecodedObjectFromState(string encodedState, string? sourceUrl = null)
        {
            return authManager.GetDecodedObjectFromState(encodedState, sourceUrl);
        }
    }

    public class PkceUtil
    {
        // Generates a random code verifier (a high-entropy cryptographic random string)
        public static string GenerateCodeVerifier()
        {
            // 32 bytes gives us 256 bits of entropy which is plenty
            var randomBytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomBytes);
            }
            return Base64UrlEncode(randomBytes);
        }

        // Computes the code challenge based on the verifier by SHA256 hashing and base64-url encoding
        public static string GenerateCodeChallenge(string codeVerifier)
        {
            using (var sha256 = SHA256.Create())
            {
                var bytes = Encoding.ASCII.GetBytes(codeVerifier);
                var hashBytes = sha256.ComputeHash(bytes);
                return Base64UrlEncode(hashBytes);
            }
        }

        // Helper to perform base64 URL encoding
        private static string Base64UrlEncode(byte[] input)
        {
            var output = Convert.ToBase64String(input)
                .TrimEnd('=')          // Remove any trailing '='s
                .Replace('+', '-')     // 62nd char of encoding
                .Replace('/', '_');    // 63rd char of encoding
            return output;
        }
    }

}

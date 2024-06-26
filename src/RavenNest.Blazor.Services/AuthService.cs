﻿using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using RavenNest.Blazor.Services.Models;
using RavenNest.BusinessLogic;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Extensions;
using RavenNest.BusinessLogic.Game;
using RavenNest.Models;
using RavenNest.Sessions;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        public async Task<SessionInfo> TwitchLoginAsync(string accessToken)
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

                    if (!u.UserName.Equals(user.Login, System.StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(user.Login))
                    {
                        u.UserName = user.Login;
                        sessionInfo.UserName = user.Login;
                        sessionInfo.UserNameChanged = true;
                    }

                    var twitchUserAccess = gameData.GetUserAccess(u.Id, "twitch");
                    if (twitchUserAccess != null)
                    {
                        twitchUserAccess.PlatformUsername = user.Login; // in case username was changed, always keep this one up to date.
                        twitchUserAccess.AccessToken = accessToken;
                        twitchUserAccess.Updated = System.DateTime.UtcNow;
                    }

                    sessionInfo.Patreon = ModelMapper.Map(gameData.GetPatreonUser(u.Id));

                    gameData.SetUserProperty(u.Id, UserProperties.Twitch_PubSub, accessToken);

                    await ravenbotApi.UpdateUserSettingsAsync(u.Id);

                    //await ravenbotApi.SendTwitchPubSubAccessTokenAsync(user.Id, user.Login, accessToken);
                }

                await logoService.UpdateUserLogosAsync(user);
            }

            return sessionInfo;
        }

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

        public string GetTwitchLoginUrl(string redirectToAfterLogin = "")
        {
            //could move List to parameters for passing more parameters for twitch to give back. This is an odd way of doing it but bonus effect
            //of adding some protection against CSRF
            List<RavenNest.Models.StateParameters> StateParametersList = new();
            if (!string.IsNullOrEmpty(redirectToAfterLogin))
                StateParametersList.Add(new("redirect", redirectToAfterLogin));

            if (Context == null || Context.Request == null || Context.Request.Host == null)
            {
                return $"https://id.twitch.tv/oauth2/authorize?client_id={settings.TwitchClientId}&redirect_uri="
                    + "https://www.ravenfall.stream/login/twitch"
                    + "&response_type=token&scope=user:read:email+bits:read+channel:read:subscriptions+channel:read:redemptions"
                    + "&state=" + GetRandomizedBase64EncodedStateParameters(StateParametersList);
            }

            return $"https://id.twitch.tv/oauth2/authorize?client_id={settings.TwitchClientId}&redirect_uri="
                    + $"https://{Context.Request.Host}/login/twitch"
                    + "&response_type=token&scope=user:read:email+bits:read+channel:read:subscriptions+channel:read:redemptions"
                    + "&state=" + GetRandomizedBase64EncodedStateParameters(StateParametersList);
        }

        //Create a 64BaseEncodedString of a JSON Object for Twitch to return back to us
        public string GetRandomizedBase64EncodedStateParameters(List<RavenNest.Models.StateParameters> stateParameters)
        {
            return authManager.GetRandomizedBase64EncodedStateParameters(stateParameters);
        }


        public List<RavenNest.Models.StateParameters> GetDecodedObjectFromState(string encodedState)
        {
            return authManager.GetDecodedObjectFromState(encodedState);
        }
    }
}

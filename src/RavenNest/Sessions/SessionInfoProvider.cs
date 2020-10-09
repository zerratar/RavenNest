using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RavenNest.BusinessLogic;
using RavenNest.BusinessLogic.Data;
using RavenNest.DataModels;
using RavenNest.Models;
using RavenNest.Twitch;

namespace RavenNest.Sessions
{
    public class SessionInfoProvider : ISessionInfoProvider
    {
        private const string TwitchAccessToken = "twitch_access_token";
        private const string TwitchUser = "twitch_user";
        private const string AuthState = "auth_state";
        private const string AuthToken = "auth_token";

        private readonly ILogger logger;
        private readonly IGameData gameData;
        private readonly AppSettings settings;

        public SessionInfoProvider(
            ILogger<SessionInfoProvider> logger, IRavenfallDbContextProvider dbProvider,
            IOptions<AppSettings> settings,
            IGameData gameData)
        {
            this.logger = logger;
            this.gameData = gameData;
            this.settings = settings.Value;
        }

        public bool TryGetAuthToken(ISession session, out AuthToken authToken)
        {
            authToken = null;
            if (session == null) return false;
            if (session.Keys.Contains(AuthToken))
            {
                authToken = JSON.Parse<AuthToken>(session.GetString(AuthToken));
            }

            return authToken != null;
        }

        public bool TryGetTwitchToken(ISession session, out string token)
        {
            token = null;
            if (session == null) return false;
            if (session.Keys.Contains(TwitchAccessToken))
            {
                token = session.GetString(TwitchAccessToken);
            }

            return !string.IsNullOrEmpty(token);
        }

        public async Task<SessionInfo> StoreAsync(ISession session)
        {
            var si = new SessionInfo();
            User user = null;

            if (TryGetTwitchToken(session, out var token))
            {
                var twitchUser = await GetTwitchUserAsync(session, token);
                if (twitchUser != null)
                {
                    user = gameData.GetUser(twitchUser.Id);
                }
                if (user != null)
                {
                    if (!string.IsNullOrEmpty(twitchUser.DisplayName) && twitchUser.DisplayName != user.DisplayName)
                    {
                        user.DisplayName = twitchUser.DisplayName;
                    }

                    if (!string.IsNullOrEmpty(twitchUser.Email) && twitchUser.Email != user.Email)
                    {
                        user.Email = twitchUser.Email;
                    }
                }
            }

            if (user == null && TryGetAuthToken(session, out var auth))
            {
                user = gameData.GetUser(auth.UserId);
            }

            if (user != null)
            {
                si.Id = user.Id;
                si.Authenticated = true;
                si.Administrator = user.IsAdmin.GetValueOrDefault();
                si.Moderator = user.IsModerator.GetValueOrDefault();
                si.UserId = user.UserId;
                si.UserName = user.UserName;
                si.RequiresPasswordChange = string.IsNullOrEmpty(user.PasswordHash);
            }

            var sessionState = JSON.Stringify(si);
            //await logger.WriteDebugAsync("SET SESSION STATE (" + session.Id + "): " + sessionState);

            session.SetString(AuthState, sessionState);
            await session.CommitAsync();
            return si;
        }

        public bool TryGet(ISession session, out SessionInfo sessionInfo)
        {
            sessionInfo = new SessionInfo();
            var json = session.GetString(AuthState);


            //logger.WriteDebug("GET SESSION STATE (" + session.Id + "): " + json);

            if (string.IsNullOrEmpty(json))
            {
                return false;
            }

            try
            {
                sessionInfo = JSON.Parse<SessionInfo>(json);
            }
            catch (Exception exc)
            {
                logger.LogError("GET SESSION STATE (" + session.Id + "): " + json + " --- PARSE ERROR (EXCEPTION): " + exc);
            }
            return true;
        }

        public void Clear(ISession session)
        {
            session.Clear();
        }

        public async Task<SessionInfo> SetTwitchTokenAsync(ISession session, string token)
        {
            session.SetString(TwitchAccessToken, token);
            await session.CommitAsync();
            return await this.StoreAsync(session);
        }

        public async Task<SessionInfo> SetTwitchUserAsync(ISession session, string twitchUser)
        {
            session.SetString(TwitchUser, twitchUser);
            await session.CommitAsync();
            return await StoreAsync(session);
        }

        public async Task<SessionInfo> SetAuthTokenAsync(ISession session, AuthToken token)
        {
            session.SetString(AuthToken, JSON.Stringify(token));
            await session.CommitAsync();
            return await StoreAsync(session);
        }

        public async Task<TwitchRequests.TwitchUser> GetTwitchUserAsync(ISession session, string token = null)
        {
            var str = session.GetString(TwitchUser);
            if (string.IsNullOrEmpty(str) && !string.IsNullOrEmpty(token))
            {
                var twitch = new TwitchRequests(token, settings.TwitchClientId, settings.TwitchClientSecret);
                var user = await twitch.GetUserAsync();
                if (user != null)
                {
                    await SetTwitchUserAsync(session, user);
                }
                str = user;
            }

            if (string.IsNullOrEmpty(str))
            {
                return null;
            }

            try
            {
                return JSON.Parse<TwitchRequests.TwitchUserData>(str).Data?.FirstOrDefault();
            }
            catch (Exception exc)
            {
                logger.LogError("GET TWITCH USER (" + session.Id + "): " + str + " --- PARSE ERROR (EXCEPTION): " + exc);
                return JSON.Parse<TwitchRequests.TwitchUser>(str);
            }
        }
    }
}

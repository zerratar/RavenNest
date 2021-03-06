﻿using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RavenNest.BusinessLogic;
using RavenNest.BusinessLogic.Data;
using RavenNest.DataModels;
using RavenNest.Models;
using RavenNest.Twitch;
using RavenNest.BusinessLogic.Game;

namespace RavenNest.Sessions
{
    public class SessionInfoProvider : ISessionInfoProvider
    {
        private const string CookieDisclaimer = "cookie_disclaimer";
        private const string TwitchAccessToken = "twitch_access_token";
        private const string TwitchUser = "twitch_user";
        private const string AuthState = "auth_state";
        private const string AuthToken = "auth_token";

        private readonly ILogger logger;
        private readonly IGameData gameData;
        private readonly IAuthManager authManager;
        private readonly AppSettings settings;

        // work around for blazor... we have to store all session data to memory. Y U LITTLE. Using the
        // actual distributed session does not work.
        private readonly ConcurrentDictionary<string, SessionData> sessions
            = new ConcurrentDictionary<string, SessionData>();

        public SessionInfoProvider(
            ILogger<SessionInfoProvider> logger,
            IOptions<AppSettings> settings,
            IGameData gameData,
            IAuthManager authManager)
        {
            this.logger = logger;
            this.gameData = gameData;
            this.authManager = authManager;
            this.settings = settings.Value;
        }

        public bool TryGetAuthToken(string sessionId, out AuthToken authToken)
        {
            authToken = null;
            if (sessionId == null) return false;
            if (ContainsKey(sessionId, AuthToken))
            {
                authToken = JSON.Parse<AuthToken>(GetString(sessionId, AuthToken));
            }

            return authToken != null;
        }

        public bool TryGetTwitchToken(string sessionId, out string token)
        {
            token = null;
            if (sessionId == null) return false;
            if (ContainsKey(sessionId, TwitchAccessToken))
            {
                token = GetString(sessionId, TwitchAccessToken);
            }

            return !string.IsNullOrEmpty(token);
        }

        public void SetActiveCharacter(SessionInfo session, Guid id)
        {
            session.ActiveCharacterId = id;
            var sessionState = JSON.Stringify(session);
            SetString(session.SessionId, AuthState, sessionState);
        }

        public void SetCookieDisclaimer(SessionInfo session, bool accepted)
        {
            session.AcceptedCookiesDisclaimer = accepted;
            var sessionState = JSON.Stringify(session);
            SetString(session.SessionId, AuthState, sessionState);
        }

        /// <summary>
        /// Only used with the twitch extension
        /// </summary>
        /// <param name="twitchUserId"></param>
        /// <returns></returns>
        public async Task<SessionInfo> CreateTwitchUserSessionAsync(string sessionId, string broadcasterId, string twitchUserId)
        {
            var si = new SessionInfo();

            if (string.IsNullOrEmpty(twitchUserId) || string.IsNullOrEmpty(broadcasterId))
                return si;

            if (twitchUserId.StartsWith("u", StringComparison.OrdinalIgnoreCase))
                twitchUserId = twitchUserId.Substring(1); // remove starting U

            var broadcaster = gameData.GetUser(broadcasterId);
            if (broadcaster == null)
                return new SessionInfo();

            var user = gameData.GetUser(twitchUserId);
            if (user == null)
                return new SessionInfo();

            //var activeSession = gameData.GetActiveSessions().FirstOrDefault(x => x.UserId == broadcaster.Id);
            var activeSession = gameData.GetSessions().FirstOrDefault(x => x.UserId == broadcaster.Id);
            if (activeSession == null)
                return new SessionInfo();

            var activeCharacter = gameData.GetCharacterBySession(activeSession.Id, twitchUserId, false);
            if (activeCharacter != null)
                si.ActiveCharacterId = activeCharacter.Id;

            si.SessionId = sessionId;
            UpdateSessionInfoData(si, user);

            var sessionState = JSON.Stringify(si);
            SetString(sessionId, AuthState, sessionState);
            return si;
        }

        public async Task<SessionInfo> StoreAsync(string sessionId)
        {
            var si = new SessionInfo();
            User user = null;

            if (TryGetTwitchToken(sessionId, out var token))
            {
                var twitchUser = await GetTwitchUserAsync(sessionId, token);
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

            if (user == null && TryGetAuthToken(sessionId, out var auth))
            {
                user = gameData.GetUser(auth.UserId);
            }

            si.SessionId = sessionId;
            UpdateSessionInfoData(si, user);

            var sessionState = JSON.Stringify(si);
            //await logger.WriteDebugAsync("SET SESSION STATE (" + session.Id + "): " + sessionState);

            SetString(sessionId, AuthState, sessionState);
            return si;
        }

        private void UpdateSessionInfoData(SessionInfo si, User user)
        {
            if (user != null)
            {
                si.Id = user.Id;
                si.Authenticated = true;
                si.AuthToken = authManager.GenerateAuthToken(user);
                si.Administrator = user.IsAdmin.GetValueOrDefault();
                si.Moderator = user.IsModerator.GetValueOrDefault();
                si.UserId = user.UserId;
                si.UserName = user.UserName;

                var clan = gameData.GetClanByUser(user.Id);
                if (clan != null)
                {
                    si.CanChangeClanName = clan.CanChangeName || clan.NameChangeCount < 2;
                }

                si.RequiresPasswordChange = string.IsNullOrEmpty(user.PasswordHash);
                si.Tier = user.PatreonTier ?? 0;

                var playSessions = new List<CharacterGameSession>();
                var myChars = gameData.GetCharacters(x => x.UserId == user.Id);
                foreach (var myChar in myChars)
                {
                    if (myChar.UserIdLock != null)
                    {
                        var skills = gameData.GetCharacterSkills(myChar.SkillsId);
                        var owner = gameData.GetUser(myChar.UserIdLock.Value);
                        var session = gameData.GetJoinedSessionByUserId(owner.UserId);
                        if (session != null)
                        {
                            playSessions.Add(new CharacterGameSession
                            {
                                CharacterId = myChar.Id,
                                CharacterCombatLevel = GetCombatLevel(skills),
                                CharacterIndex = myChar.CharacterIndex,
                                CharacterName = myChar.Name,
                                SessionTwitchUserId = owner.UserId,
                                SessionTwitchUserName = owner.UserName,
                                Joined = myChar.LastUsed.GetValueOrDefault()
                            });
                        }
                    }
                }
                si.PlaySessions = playSessions;
            }
        }
        public int GetCombatLevel(DataModels.Skills skills)
        {
            return (int)(((skills.AttackLevel + skills.DefenseLevel + skills.HealthLevel + skills.StrengthLevel) / 4f) +
                   ((skills.RangedLevel + skills.MagicLevel + skills.HealingLevel) / 8f));
        }

        public bool TryGet(string sessionId, out SessionInfo sessionInfo)
        {
            sessionInfo = new SessionInfo();
            var json = GetString(sessionId, AuthState);

            if (string.IsNullOrEmpty(json))
            {
                return false;
            }

            try
            {
                sessionInfo = JSON.Parse<SessionInfo>(json);
                if (sessionInfo.Authenticated)
                {
                    var user = gameData.GetUser(sessionInfo.UserId);
                    UpdateSessionInfoData(sessionInfo, user);
                }
            }
            catch (Exception exc)
            {
                logger.LogError("GET SESSION STATE (" + sessionId + "): " + json + " --- PARSE ERROR (EXCEPTION): " + exc);
            }
            return true;
        }

        public void Clear(string sessionId)
        {
            GetSessionData(sessionId).Clear();
        }

        public async Task<SessionInfo> SetTwitchTokenAsync(string sessionId, string token)
        {
            SetString(sessionId, TwitchAccessToken, token);
            return await this.StoreAsync(sessionId);
        }

        public async Task<SessionInfo> SetTwitchUserAsync(string sessionId, string twitchUser)
        {
            SetString(sessionId, TwitchUser, twitchUser);
            return await StoreAsync(sessionId);
        }

        public async Task<SessionInfo> SetAuthTokenAsync(string sessionId, AuthToken token)
        {
            SetString(sessionId, AuthToken, JSON.Stringify(token));
            return await StoreAsync(sessionId);
        }

        public async Task<TwitchRequests.TwitchUser> GetTwitchUserAsync(string sessionId, string token = null)
        {
            var str = GetString(sessionId, TwitchUser);
            if (string.IsNullOrEmpty(str) && !string.IsNullOrEmpty(token))
            {
                var twitch = new TwitchRequests(token, settings.TwitchClientId, settings.TwitchClientSecret);
                var user = await twitch.GetUserAsync();
                if (user != null)
                {
                    await SetTwitchUserAsync(sessionId, user);
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
                logger.LogError("GET TWITCH USER (" + sessionId + "): " + str + " --- PARSE ERROR (EXCEPTION): " + exc);
                return JSON.Parse<TwitchRequests.TwitchUser>(str);
            }
        }

        private bool ContainsKey(string sessionId, string key)
        {
            var data = GetSessionData(sessionId);
            return data.Data.ContainsKey(key);
        }

        private string GetString(string sessionId, string key)
        {
            return GetSessionData(sessionId).GetString(key);
        }

        private void SetString(string sessionId, string key, string value)
        {
            GetSessionData(sessionId).SetString(key, value);
        }

        private SessionData GetSessionData(string sessionId)
        {
            if (string.IsNullOrEmpty(sessionId))
                return new SessionData();

            if (!sessions.TryGetValue(sessionId, out var data))
            {
                data = new SessionData();
                sessions[sessionId] = data;
            }
            else
            {
                // Check if session timed out, if so. clear it. Even if we use the same session cookie id
                var idleTime = DateTime.UtcNow - data.LastAccessed;
                if (idleTime >= SessionCookie.SessionTimeout)
                {
                    data = new SessionData();
                    sessions[sessionId] = data;
                }
            }

            return data;
        }
    }
}

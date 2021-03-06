﻿using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using RavenNest.Models;
using RavenNest.Twitch;

namespace RavenNest.Sessions
{
    public interface ISessionInfoProvider
    {
        void Clear(string sessionId);
        bool TryGet(string sessionId, out SessionInfo sessionInfo);
        bool TryGetTwitchToken(string sessionId, out string token);
        bool TryGetAuthToken(string sessionId, out AuthToken authToken);
        Task<TwitchRequests.TwitchUser> GetTwitchUserAsync(string sessionId, string token = null);
        Task<SessionInfo> SetTwitchUserAsync(string sessionId, string twitchUser);
        Task<SessionInfo> SetTwitchTokenAsync(string sessionId, string token);
        Task<SessionInfo> SetAuthTokenAsync(string sessionId, AuthToken token);
        Task<SessionInfo> StoreAsync(string sessionId);
        Task<SessionInfo> CreateTwitchUserSessionAsync(string sessionId, string broadcasterId, string twitchUserId);
        void SetActiveCharacter(SessionInfo session, Guid id);
        void SetCookieDisclaimer(SessionInfo session, bool accepted);
    }
}

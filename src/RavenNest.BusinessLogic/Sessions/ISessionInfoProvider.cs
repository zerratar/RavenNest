using System;
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
        bool TryGet(Guid characterId, out SessionInfo sessionInfo);
        bool TryGetTwitchToken(string sessionId, out string token);
        bool TryGetAuthToken(string sessionId, out AuthToken authToken);
        Task<TwitchRequests.TwitchUser> GetTwitchUserAsync(string sessionId, string token = null);
        Task<TwitchUserSessionInfo> SetTwitchUserAsync(string sessionId, string twitchUser);
        Task<TwitchUserSessionInfo> SetTwitchTokenAsync(string sessionId, string token);
        Task<TwitchUserSessionInfo> SetAuthTokenAsync(string sessionId, AuthToken token);
        Task<TwitchUserSessionInfo> StoreAsync(string sessionId);
        Task<SessionInfo> CreateTwitchUserSessionAsync(string sessionId, string broadcasterId, string twitchUserId);
        void SetActiveCharacter(SessionInfo session, Guid? id);
        void SetCookieDisclaimer(SessionInfo session, bool accepted);
    }
}

using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using RavenNest.Models;
using RavenNest.Twitch;

namespace RavenNest.Sessions
{
    public interface ISessionInfoProvider
    {
        void Clear(ISession session);
        bool TryGet(ISession session, out SessionInfo sessionInfo);
        bool TryGetTwitchToken(ISession session, out string token);
        bool TryGetAuthToken(ISession session, out AuthToken authToken);
        Task<TwitchRequests.TwitchUser> GetTwitchUserAsync(ISession session, string token = null);
        Task<SessionInfo> SetTwitchUserAsync(ISession session, string twitchUser);
        Task<SessionInfo> SetTwitchTokenAsync(ISession session, string token);
        Task<SessionInfo> SetAuthTokenAsync(ISession session, AuthToken token);
        Task<SessionInfo> StoreAsync(ISession session);
    }
}
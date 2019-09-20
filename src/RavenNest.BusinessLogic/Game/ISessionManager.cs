using System.Threading.Tasks;
using RavenNest.Models;

namespace RavenNest.BusinessLogic.Game
{
    public interface ISessionManager
    {
        Task<SessionToken> BeginSessionAsync(AuthToken token, string clientVersion, string accessKey, bool isLocal);
        SessionToken Get(string sessionToken);
        Task EndSessionAsync(SessionToken token);
        Task<bool> EndSessionAndRaidAsync(SessionToken token, string userIdOrUsername, bool isWarRaid);
    }
}
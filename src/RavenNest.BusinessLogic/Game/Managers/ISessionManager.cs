using System;
using System.Threading.Tasks;
using RavenNest.Models;

namespace RavenNest.BusinessLogic.Game
{
    public interface ISessionManager
    {
        Task<SessionToken> BeginSessionAsync(
            AuthToken token,
            string clientVersion,
            string accessKey,
            bool isLocal,
            float syncTime);

        SessionToken Get(string sessionToken);
        void SendVillageInfo(DataModels.GameSession newGameSession);
        Task SendPermissionDataAsync(DataModels.GameSession gameSession, DataModels.User user = null);
        void EndSession(SessionToken token);
        bool EndSessionAndRaid(SessionToken token, string userIdOrUsername, bool isWarRaid);
        bool AttachPlayersToSession(SessionToken session, Guid[] characterIds);
    }
}

using System;
using System.Threading.Tasks;
using RavenNest.BusinessLogic.Net;
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
        void SendPermissionData(DataModels.GameSession gameSession, DataModels.User user = null);
        void EndSession(SessionToken token);
        void RecordTimeMismatch(SessionToken sessionToken, TimeSyncUpdate update);
        bool EndSessionAndRaid(SessionToken token, string userIdOrUsername, bool isWarRaid);
        bool AttachPlayersToSession(SessionToken session, Guid[] characterIds);
        void SendExpMultiplier(DataModels.GameSession session);
        void SendServerTime(DataModels.GameSession session);
    }
}

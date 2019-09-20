using RavenNest.Models;

namespace RavenNest.BusinessLogic.Game
{
    public interface IPlayerSessionManager
    {
        bool TryGetActiveStream(string playerUserId, out string streamerUserId);
        void JoinStream(SessionToken token, string playerUserId);
        void LeaveStream(SessionToken token, string playerUserId);
    }
}
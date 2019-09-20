using RavenNest.Models;

namespace RavenNest.BusinessLogic.Game
{
    public class PlayerSessionManager : IPlayerSessionManager
    {
        public bool TryGetActiveStream(string playerUserId, out string streamerUserId)
        {
            streamerUserId = null;
            return false;
        }

        public void JoinStream(SessionToken token, string playerUserId)
        {
        }

        public void LeaveStream(SessionToken token, string playerUserId)
        {
        }
    }
}
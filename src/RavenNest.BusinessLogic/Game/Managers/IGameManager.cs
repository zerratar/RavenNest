using System.Threading.Tasks;
using RavenNest.Models;

namespace RavenNest.BusinessLogic.Game
{
    public interface IGameManager
    {
        GameInfo GetGameInfo(SessionToken session);
        EventCollection GetGameEvents(SessionToken session);
        bool Join(string userId, string targetUserId);
        bool Leave(string userId);
        bool SetTask(string userId, string task, string taskArgument);
        bool JoinRaid(string userId);
        bool JoinDungeon(string userId);
        bool JoinArena(string userId);
    }
}
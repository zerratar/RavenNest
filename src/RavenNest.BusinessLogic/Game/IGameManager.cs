using System.Threading.Tasks;
using RavenNest.Models;

namespace RavenNest.BusinessLogic.Game
{
    public interface IGameManager
    {
        GameInfo GetGameInfo(SessionToken session);
        EventCollection GetGameEvents(SessionToken session);
    }
}
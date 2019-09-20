using System.Threading.Tasks;
using RavenNest.Models;

namespace RavenNest.BusinessLogic.Game
{
    public interface IGameManager
    {
        Task<GameInfo> GetGameInfoAsync(SessionToken session);
        Task<EventCollection> GetGameEventsAsync(SessionToken session, int revision);
    }
}
using System.Threading.Tasks;
using RavenNest.Models;

namespace RavenNest.SDK.Endpoints
{
    public interface IGameEndpoint
    {
        Task<GameInfo> GetAsync();

        Task<SessionToken> BeginSessionAsync(bool local);

        Task<bool> EndSessionAndRaidAsync(string username, bool war);

        Task EndSessionAsync();

        Task<EventCollection> PollEventsAsync(int revision);
    }
}
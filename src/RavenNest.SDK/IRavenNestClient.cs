using System.Threading.Tasks;
using RavenNest.Models;
using RavenNest.SDK.Endpoints;

namespace RavenNest.SDK
{
    public interface IRavenNestClient
    {
        IAuthEndpoint Auth { get; }
        IGameEndpoint Game { get; }
        IItemEndpoint Items { get; }
        IPlayerEndpoint Players { get; }

        Task<bool> LoginAsync(string username, string password);
        Task<bool> StartSessionAsync(bool useLocalPlayers);
        Task<bool> EndSessionAsync();
        Task<bool> EndSessionAndRaidAsync(string username, bool war);

        Task<Player> PlayerJoinAsync(string userId, string username);

        bool Authenticated { get; }
        bool SessionStarted { get; }
    }
}
using RavenNest.BusinessLogic.Models.Patreon.API;
using RavenNest.DataModels;
using RavenNest.Models;
using RavenNest.Sessions;
using System.Threading.Tasks;

namespace RavenNest.BusinessLogic.Game
{
    public interface IPatreonManager
    {
        Task<UserPatreon> LinkAsync(SessionInfo session, string code);
        Task<PatreonTier> GetTierByCentsAsync(decimal pledgeAmountCents);
        Task<PatreonTier> GetTierByLevelAsync(int tierLevel);
        string GetRedirectUrl();
        void Unlink(SessionInfo session);
    }
}

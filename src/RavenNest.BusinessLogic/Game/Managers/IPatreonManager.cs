using RavenNest.BusinessLogic.Patreon;

namespace RavenNest.BusinessLogic.Game
{
    public interface IPatreonManager
    {
        void AddPledge(PatreonPledgeData data);
        void UpdatePledge(PatreonPledgeData data);
        void RemovePledge(PatreonPledgeData data);
    }
}

using RavenNest.BusinessLogic.Patreon;

namespace RavenNest.BusinessLogic.Game
{
    public interface IPatreonManager
    {
        void AddPledge(IPatreonData data);
        void UpdatePledge(IPatreonData data);
        void RemovePledge(IPatreonData data);
    }
}

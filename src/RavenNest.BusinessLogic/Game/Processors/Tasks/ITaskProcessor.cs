using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Providers;
using RavenNest.DataModels;

namespace RavenNest.BusinessLogic.Game.Processors.Tasks
{
    public interface ITaskProcessor
    {
        void Handle(
            IIntegrityChecker integrityChecker, 
            IGameData gameData,
            IPlayerInventoryProvider inventoryProvider,
            GameSession session, 
            Character character, 
            CharacterState state);
    }
}

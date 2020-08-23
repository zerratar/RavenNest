using RavenNest.BusinessLogic.Data;
using RavenNest.DataModels;

namespace RavenNest.BusinessLogic.Game.Processors.Tasks
{
    public class CookingTaskProcessor : ResourceTaskProcessor
    {
        public override void Handle(
            IIntegrityChecker integrityChecker, 
            IGameData gameData,
            IPlayerInventoryProvider inventoryProvider,
            DataModels.GameSession session,
            Character character, 
            CharacterState state)
        {
        }
    }
}

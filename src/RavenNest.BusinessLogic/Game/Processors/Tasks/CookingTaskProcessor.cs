using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Providers;
using RavenNest.DataModels;

namespace RavenNest.BusinessLogic.Game.Processors.Tasks
{
    public class CookingTaskProcessor : ResourceTaskProcessor
    {
        public override void Process(
            IIntegrityChecker integrityChecker, 
            GameData gameData,
            PlayerInventoryProvider inventoryProvider,
            DataModels.GameSession session,
            Character character, 
            CharacterState state)
        {
        }
    }
}

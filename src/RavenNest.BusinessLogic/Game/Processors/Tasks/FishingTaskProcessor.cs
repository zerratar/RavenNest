using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Providers;
using RavenNest.DataModels;

namespace RavenNest.BusinessLogic.Game.Processors.Tasks
{
    public class FishingTaskProcessor : ResourceTaskProcessor
    {
        public override void Process(
            IIntegrityChecker integrityChecker, 
            IGameData gameData,
            IPlayerInventoryProvider inventoryProvider,
            DataModels.GameSession session, 
            Character character, 
            CharacterState state)
        {
            UpdateResourceGain(integrityChecker, gameData, inventoryProvider, session, character, resources =>
            {
                ++resources.Fish;
                var villageResources = GetVillageResources(gameData, session);
                if (villageResources != null)
                {
                    ++villageResources.Fish;
                }
            });
        }
    }
}

using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Providers;
using RavenNest.DataModels;

namespace RavenNest.BusinessLogic.Game.Processors.Tasks
{
    public class WoodcuttingTaskProcessor : ResourceTaskProcessor
    {
        public override void Handle(
            IIntegrityChecker integrityChecker,
            IGameData gameData,
            IPlayerInventoryProvider inventoryProvider,
            GameSession session,
            Character character,
            CharacterState state)
        {
            UpdateResourceGain(integrityChecker, gameData, inventoryProvider, session, character, resources =>
            {
                ++resources.Wood;
                var villageResources = GetVillageResources(gameData, session);
                if (villageResources != null)
                {
                    ++villageResources.Wood;
                }
            });
        }
    }
}

using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Data;
using RavenNest.DataModels;

namespace RavenNest.BusinessLogic.Game.Processors.Tasks
{
    public class AlchemyTaskProcessor : ResourceTaskProcessor
    {
        public override void Process(
            ILogger logger,
            GameData gameData,
            PlayerInventoryProvider inventoryProvider,
            GameSession session,
            Character character,
            CharacterState state)
        {
        }
    }
}

using RavenNest.BusinessLogic.Data;
using RavenNest.DataModels;

namespace RavenNest.BusinessLogic.Game.Processors.Tasks
{
    public class FarmingTaskProcessor : ResourceTaskProcessor
    {
        public override void Handle(
            IIntegrityChecker integrityChecker, 
            IGameData gameData, 
            GameSession session, 
            Character character, 
            CharacterState state)
        {
            UpdateResourceGain(integrityChecker, gameData, session, character, resources => ++resources.Wheat);
        }
    }
}

using RavenNest.BusinessLogic.Data;
using RavenNest.DataModels;

namespace RavenNest.BusinessLogic.Game.Processors.Tasks
{
    public class FightingTaskProcessor : ITaskProcessor
    {
        public void Handle(
            IIntegrityChecker integrityChecker,
            IGameData gameData, 
            GameSession session, 
            Character character, 
            CharacterState state)
        {
        }
    }
}

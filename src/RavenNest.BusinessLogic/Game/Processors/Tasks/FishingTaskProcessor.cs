using RavenNest.BusinessLogic.Data;
using RavenNest.DataModels;

namespace RavenNest.BusinessLogic.Game.Processors.Tasks
{
    public class FishingTaskProcessor : ResourceTaskProcessor
    {
        public override void Handle(IGameData gameData, GameSession session, Character character, CharacterState state)
        {
            UpdateResourceGain(gameData, session, character, resources => ++resources.Fish);
        }
    }
}

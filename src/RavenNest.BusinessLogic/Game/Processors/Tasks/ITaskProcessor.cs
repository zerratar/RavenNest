using RavenNest.BusinessLogic.Data;
using RavenNest.DataModels;

namespace RavenNest.BusinessLogic.Game.Processors.Tasks
{
    public interface ITaskProcessor
    {
        void Handle(IGameData gameData, GameSession session, Character character, CharacterState state);
    }
}

using RavenNest.BusinessLogic.Data;
using RavenNest.DataModels;

namespace RavenNest.BusinessLogic.Game.Processors.Tasks
{
    public class GatheringTaskProcessor : ResourceTaskProcessor
    {
        public static readonly SimpleDropHandler Drops = new SimpleDropHandler(nameof(Skills.Gathering));

        public override void Process(
            GameData gameData,
            PlayerInventoryProvider inventoryProvider,
            GameSession session,
            Character character,
            CharacterState state)
        {
            UpdateResourceGain(gameData, session, character, resources =>
            {
                var skills = gameData.GetCharacterSkills(character.SkillsId);
                if (skills == null)
                    return;

                Drops.TryDropItem(this, gameData, inventoryProvider, session, character, skills.GatheringLevel);
            });
        }
    }
}

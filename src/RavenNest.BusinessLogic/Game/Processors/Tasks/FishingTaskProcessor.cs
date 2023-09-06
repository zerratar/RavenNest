using RavenNest.BusinessLogic.Data;
using RavenNest.DataModels;

namespace RavenNest.BusinessLogic.Game.Processors.Tasks
{
    public class FishingTaskProcessor : ResourceTaskProcessor
    {
        public static readonly SimpleDropHandler Drops = new SimpleDropHandler(nameof(Skills.Fishing));

        public override void Process(
            GameData gameData,
            PlayerInventoryProvider inventoryProvider,
            GameSession session,
            Character character,
            CharacterState state)
        {
            UpdateResourceGain(gameData, session, character, resources =>
            {
                var villageResources = GetVillageResources(gameData, session);
                if (villageResources != null)
                {
                    ++villageResources.Fish;
                }

                var skills = gameData.GetCharacterSkills(character.SkillsId);
                if (skills == null)
                    return;

                Drops.TryDropItem(this, gameData, inventoryProvider, session, character, skills.FishingLevel);
            });
        }
    }
}

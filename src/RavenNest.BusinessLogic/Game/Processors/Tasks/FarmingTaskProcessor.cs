using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Providers;
using RavenNest.DataModels;

namespace RavenNest.BusinessLogic.Game.Processors.Tasks
{
    public class FarmingTaskProcessor : ResourceTaskProcessor
    {
        public static readonly SimpleDropHandler Drops = new SimpleDropHandler(nameof(Skills.Farming));

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
                ++resources.Wheat;
                var villageResources = GetVillageResources(gameData, session);
                if (villageResources != null)
                {
                    ++villageResources.Wheat;
                }

                var skills = gameData.GetCharacterSkills(character.SkillsId);
                if (skills == null)
                    return;

                Drops.TryDropItem(this, gameData, inventoryProvider, session, character, skills.FarmingLevel);
            });
        }
    }
}

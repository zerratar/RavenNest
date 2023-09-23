using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Data;
using RavenNest.DataModels;
using System;

namespace RavenNest.BusinessLogic.Game.Processors.Tasks
{
    public class WoodcuttingTaskProcessor : ResourceTaskProcessor
    {
        public static readonly SimpleDropHandler Drops = new SimpleDropHandler(nameof(Skills.Woodcutting));

        public override void Process(
            ILogger logger,
            GameData gameData,
            PlayerInventoryProvider inventoryProvider,
            GameSession session,
            Character character,
            CharacterState state)
        {
            UpdateResourceGain(gameData, session, character, resources =>
            {
                if (state.InOnsen.GetValueOrDefault() || state.InDungeon.GetValueOrDefault() || state.InRaid || string.IsNullOrEmpty(state.Island))
                    return;

                var skills = gameData.GetCharacterSkills(character.SkillsId);
                if (skills == null)
                    return;

#warning TODO: Allow for getting resources if you have bonus increased your level?
                if (!TryGetIsland(state.Island, out var island) || islandLevelRequirements[island][RavenNest.Models.Skill.Woodcutting] > skills.WoodcuttingLevel)
                    return;

                session.Updated = DateTime.UtcNow;
                var villageResources = GetVillageResources(gameData, session);
                if (villageResources != null)
                {
                    ++villageResources.Wood;
                }

                Drops.TryDropItem(this, logger, gameData, inventoryProvider, session, character, skills.WoodcuttingLevel, state.TaskArgument);
            });
        }
    }
}

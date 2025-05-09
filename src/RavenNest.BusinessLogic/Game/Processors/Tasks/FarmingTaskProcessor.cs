using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Data;
using RavenNest.DataModels;
using System;

namespace RavenNest.BusinessLogic.Game.Processors.Tasks
{
    public class FarmingTaskProcessor : ResourceTaskProcessor
    {
        public static readonly SimpleDropHandler Drops = new SimpleDropHandler(nameof(Skills.Farming));

        public override void Process(
            ILogger logger,
            GameData gameData,
            PlayerInventory inventory,
            GameSession session,
            User user,
            Character character,
            CharacterState state)
        {
            UpdateResourceGain(gameData, session, character, resources =>
            {
                // make sure our skill level is high enough to train here and that we are not sailing or in a dungeon
                if (state.InOnsen.GetValueOrDefault() || state.InDungeon.GetValueOrDefault() || state.InRaid || string.IsNullOrEmpty(state.Island))
                    return;

                var skills = gameData.GetCharacterSkills(character.SkillsId);
                if (skills == null)
                    return;

                var level = skills.FarmingLevel + inventory.GetFarmingBonus();
                if (!TryGetIsland(state.Island, out var island) || islandLevelRequirements[island][RavenNest.Models.Skill.Farming] > level)
                    return;

                session.Updated = DateTime.UtcNow;
                var villageResources = GetVillageResources(gameData, session);
                if (villageResources != null)
                {
                    ++villageResources.Wheat;
                }

                Drops.TryDropItem(this, logger, gameData, inventory, session, character, level, state.TaskArgument);
            });
        }
    }
}

using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Data;
using RavenNest.DataModels;
using System;

namespace RavenNest.BusinessLogic.Game.Processors.Tasks
{
    public class FishingTaskProcessor : ResourceTaskProcessor
    {
        public static readonly SimpleDropHandler Drops = new SimpleDropHandler(nameof(Skills.Fishing));

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
                if (state.InOnsen.GetValueOrDefault() || state.InDungeon.GetValueOrDefault() || state.InRaid || string.IsNullOrEmpty(state.Island))
                    return;

                var skills = gameData.GetCharacterSkills(character.SkillsId);
                if (skills == null)
                    return;

                if (!TryGetIsland(state.Island, out var island))
                {
                    // island not found, this should not happen.
                    var sessionState = gameData.GetSessionState(session.Id);
                    logger.LogError($"[{sessionState?.ClientVersion}] <Fishing> Island not found: '{state.Island}' for user {user.UserName} ({user.Id}) in session {session.Id}.");
                    return;
                }

                var level = skills.FishingLevel + inventory.GetFishingBonus();
                if (islandLevelRequirements[island][RavenNest.Models.Skill.Fishing] > level)
                    return;

                var villageResources = GetVillageResources(gameData, session);
                if (villageResources != null)
                {
                    ++villageResources.Fish;
                }

                session.Updated = DateTime.UtcNow;


                Drops.TryDropItem(this, logger, gameData, inventory, session, character, level, state.TaskArgument);
            });
        }
    }
}

using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Data;
using RavenNest.DataModels;

namespace RavenNest.BusinessLogic.Game.Processors.Tasks
{
    /// <summary>
    ///     Training a combat skill gives what creatures leave behind, the way farming gives crops: the resource drops
    ///     of the Slayer skill (Hide, for leather; class gear, v0.9.7), rolled every gathering round with the
    ///     character's Slayer level. Like every other resource it follows the task, not single kills: the server does
    ///     not see those here. Raids, dungeons, the arena and the onsen never get this far (GameProcessor).
    /// </summary>
    public class FightingTaskProcessor : ResourceTaskProcessor
    {
        public static readonly SimpleDropHandler Drops = new SimpleDropHandler(nameof(Skills.Slayer));

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

                var level = skills.SlayerLevel + inventory.GetSlayerBonus();
                Drops.TryDropItem(this, logger, gameData, inventory, session, character, level, state.TaskArgument);
            });
        }
    }
}

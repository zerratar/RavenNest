using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Providers;
using RavenNest.DataModels;
using System;

namespace RavenNest.BusinessLogic.Game.Processors.Tasks
{
    public class SailingTaskProcessor : ResourceTaskProcessor
    {
        public static readonly SimpleDropHandler Drops = new SimpleDropHandler(nameof(Skills.Sailing));

        public override void Process(
            ILogger logger,
            GameData gameData,
            PlayerInventoryProvider inventoryProvider,
            GameSession session,
            Character character,
            CharacterState state)
        {
            // if player has been sailing for certain amount of time
            // and if player does not already own one of the hats (Sailor hat, pirate hat)
            // then reward the player one of those.

            var now = DateTime.UtcNow;
            var s = gameData.GetCharacterSessionState(session.Id, character.Id);

            // note: we could make it so that it will only drop if you have been sailing for some time. Not an accumulated sailing
            //       that way its only rewarded to players that are actively training sailing.

            var inventory = inventoryProvider.Get(character.Id);

            if (s.SailingRewardAttempted == DateTime.UnixEpoch)
            {
                s.SailingRewardAttempted = now;
            }

            if (now - s.SailingRewardAttempted < TimeSpan.FromMinutes(5))//ItemDropRateSettings.ResourceGatherInterval))
            {
                return;
            }

            s.SailingRewardAttempted = now;

            var skills = gameData.GetCharacterSkills(character.SkillsId);
            if (skills == null)
                return;

            session.Updated = DateTime.UtcNow;
            Drops.TryDropItem(this, logger, gameData, inventoryProvider, session, character, skills.SailingLevel, state.TaskArgument, drop => !inventory.ContainsItem(drop.ItemId));
        }
    }
}

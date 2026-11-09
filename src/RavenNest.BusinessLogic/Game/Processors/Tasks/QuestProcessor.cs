using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Data;
using RavenNest.DataModels;
using GameEventType = RavenNest.Models.GameEventType;
using PlayStreakEgg = RavenNest.Models.PlayStreakEgg;

namespace RavenNest.BusinessLogic.Game.Processors.Tasks
{
    public class QuestProcessor : PlayerTaskProcessor
    {
        public override void Process(
            ILogger logger,
            GameData gameData,
            PlayerInventory inventory,
            GameSession session,
            User user,
            Character character,
            CharacterState state)
        {
            if (character == null || state == null)
            {
                StreamerProcess(logger, gameData, session, inventory);
                return;
            }

            PlayStreak(logger, gameData, inventory, session, character);
        }

        /// <summary>
        ///     Days played in a row, counted here because this runs for every character in a running session on every
        ///     tick: a viewer who stays in a stream for a week counts each day as one who leaves and joins daily does.
        ///     A day without play starts it again. On day 7, then every 14 days, a pet egg (the quests' table).
        /// </summary>
        private static void PlayStreak(ILogger logger, GameData gameData, PlayerInventory inventory, GameSession session, Character character)
        {
            try
            {
                var now = System.DateTime.UtcNow;
                var today = now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
                var stats = gameData.GetCharacterQuestStats(character.Id);
                if (stats != null && stats.LastPlayDay == today) return;
                if (stats == null)
                {
                    stats = new CharacterQuestStats { Id = System.Guid.NewGuid(), CharacterId = character.Id };
                    gameData.Add(stats);
                }

                var yesterday = now.AddDays(-1).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
                stats.PlayStreak = stats.LastPlayDay == yesterday ? stats.PlayStreak + 1 : 1;
                stats.LastPlayDay = today;
                if (!RavenNest.Combat.PetEggs.StreakGivesEgg(stats.PlayStreak) || inventory == null) return;

                var species = RavenNest.Combat.PetEggs.Pick(RavenNest.Combat.EggSource.Quest, 0, System.Random.Shared.NextDouble());
                if (species == null) return;
                var itemId = RavenNest.Combat.PetEggs.ItemId(species.Id);
                if (gameData.GetItem(itemId) == null) return;
                inventory.AddItem(itemId, 1);
                gameData.EnqueueGameEvent(gameData.CreateSessionEvent(GameEventType.PlayStreakEgg, session,
                    new PlayStreakEgg { CharacterId = character.Id, Days = stats.PlayStreak, Egg = species.EggName }));
            }
            catch (System.Exception exc)
            {
                logger.LogError("Play streak failed for " + character?.Name + ": " + exc);
            }
        }

        private void StreamerProcess(ILogger logger, GameData gameData, GameSession session, PlayerInventory inventory)
        {

        }
    }
}

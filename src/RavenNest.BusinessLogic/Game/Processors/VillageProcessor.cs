using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Net;
using RavenNest.DataModels;
using System;

namespace RavenNest.BusinessLogic.Game.Processors.Tasks
{
    public class VillageProcessor : PlayerTaskProcessor
    {
        private readonly TimeSpan updateExpInterval = TimeSpan.FromSeconds(15);
        private DateTime lastUpdate = DateTime.UnixEpoch;
        private DateTime lastExpSend = DateTime.UnixEpoch;

        public override void Process(
            ILogger logger,
            GameData gameData,
            PlayerInventory inventory,
            GameSession session,
            User user,
            Character character,
            CharacterState state)
        {
            //var players = gameData.GetActiveSessionCharacters(session);
            var playerCount = 750; // Fixed Rate instead. //players.Count;

            if (lastUpdate <= DateTime.UnixEpoch)
            {
                lastUpdate = DateTime.UtcNow;
            }

            var elapsed = DateTime.UtcNow - lastUpdate;
            if (playerCount == 0 && elapsed < updateExpInterval)
            {
                return;
            }

            if (elapsed > updateExpInterval)
            {
                elapsed = updateExpInterval;
            }

            var village = gameData.GetOrCreateVillageBySession(session);

            // if village is null, the user is null.
            if (village == null)
            {
                return;
            }

            var nextLevel = village.Level + 1;
            var expForNextLevel = GameMath.ExperienceForLevel(nextLevel);

            if (playerCount > 0)
            {
                var owner = gameData.GetUser(village.UserId);
                if (owner.PatreonTier >= (int)DataModels.Patreon.Mithril)
                {
                    elapsed *= 2;
                }

                var gained = GameMath.GetVillageExperience(village.Level, playerCount, elapsed);

                // The gain is checked, not the total. Checking the total was the bug: a village
                // whose experience had drifted past the requirement could never come back, because
                // the check sat above the level up loop and returned before reaching it. Every
                // later tick then added more and returned again, so the gap grew for ever and the
                // village stayed at whatever level it was stuck on. Reported as "it needs
                // -374,203,328 xp to level up", getting further negative by the day.
                if (double.IsNaN(gained) || double.IsInfinity(gained) || gained < 0)
                {
                    gained = 0;
                }

                village.Experience += gained;

                if (double.IsNaN(village.Experience) || double.IsInfinity(village.Experience))
                {
                    logger.LogError($"Village '{village.Id}' had an unusable experience value and was reset to zero.");
                    village.Experience = 0;
                }
            }

            // There is no ceiling on accumulated experience here, on purpose.
            //
            // There used to be: if the total passed twice the next level's requirement the
            // processor returned, and that return sat above the loop below, so a village that ever
            // crossed the line could never level again and every later tick pushed it further out.
            // It was guarding against a village being handed a large amount of experience at once,
            // which nothing can do: this method is the only thing in the codebase that writes
            // village.Experience, so there is no path for it to guard.
            //
            // A village that is over the line drains through the loop instead and comes out at the
            // level its experience pays for, which is what the loop was always for.

            var levelDelta = 0;
            while (village.Experience >= expForNextLevel && village.Level < GameMath.MaxVillageLevel)
            {
                village.Experience -= expForNextLevel;
                village.Level++;
                levelDelta++;
                expForNextLevel = GameMath.ExperienceForLevel(village.Level + 1);
            }

            var villageHouses = gameData.GetOrCreateVillageHouses(village);

            if (levelDelta > 0 || DateTime.UtcNow - lastExpSend > updateExpInterval)
            {
                var data = new VillageLevelUp
                {
                    Experience = (long)village.Experience,
                    Level = village.Level,
                    LevelDelta = levelDelta,
                    HouseSlots = villageHouses.Count
                };

                gameData.EnqueueGameEvent(gameData.CreateSessionEvent(RavenNest.Models.GameEventType.VillageLevelUp, session, data));
                lastExpSend = DateTime.UtcNow;
            }

            lastUpdate = DateTime.UtcNow;
        }
    }
}

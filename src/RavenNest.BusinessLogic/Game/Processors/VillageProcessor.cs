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

            // A village that is already past the requirement is repaired rather than frozen, and
            // the excess is dropped rather than spent.
            //
            // Spending it would be far too generous: the gain per tick is scaled to the cost of the
            // next level, so a village stuck at 48 accumulated at level 49 rates while the levels it
            // would buy cost level 300 prices. One reported village held 2,251,082% of its
            // requirement, which would have taken it from level 48 to 353 in one tick, and its house
            // slots from 10 to 35. Nobody earned that; the village was simply stuck while the clock
            // ran. Clamping puts it one level up and then back to levelling normally.
            if (village.Experience > expForNextLevel * 2)
            {
                logger.LogWarning(
                    $"Village '{village.Id}' was holding {village.Experience:N0} experience against a " +
                    $"requirement of {expForNextLevel:N0} at level {village.Level}. Clamped so it can level again.");

                village.Experience = expForNextLevel;
            }

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

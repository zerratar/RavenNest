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

                village.Experience += GameMath.GetVillageExperience(village.Level, playerCount, elapsed);

                // check if this village gone mad.
                var percentage = village.Experience / (double)expForNextLevel;
                if (percentage > 2)
                {
                    // this should not happen.
                    return;
                }

                if (double.IsNaN(village.Experience) || double.IsInfinity(village.Experience))
                {
                    village.Experience = 0;
                    return;
                }
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

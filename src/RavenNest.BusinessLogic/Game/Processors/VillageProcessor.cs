using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Net;
using RavenNest.DataModels;
using System;

namespace RavenNest.BusinessLogic.Game.Processors.Tasks
{
    public class VillageProcessor : PlayerTaskProcessor
    {
        private readonly TimeSpan updateExpInterval = TimeSpan.FromSeconds(15);
        private DateTime lastUpdate = DateTime.MinValue;
        private DateTime lastExpSend = DateTime.MinValue;

        public override void Process(
            GameData gameData,
            PlayerInventoryProvider inventoryProvider,
            GameSession session,
            Character character,
            CharacterState state)
        {
            var players = gameData.GetActiveSessionCharacters(session);

            if (lastUpdate == DateTime.MinValue)
            {
                lastUpdate = DateTime.UtcNow;
            }

            var elapsed = DateTime.UtcNow - lastUpdate;
            if (players.Count == 0 && elapsed < updateExpInterval)
            {
                return;
            }

            if (elapsed > updateExpInterval)
            {
                elapsed = updateExpInterval;
            }

            var village = gameData.GetOrCreateVillageBySession(session);
            var nextLevel = village.Level + 1;
            var expForNextLevel = GameMath.ExperienceForLevel(nextLevel);
            var expGain = GameMath.GetVillageExperience(village.Level, players.Count, elapsed);

            // check if this village gone mad.
            var percentage = village.Experience / (double)expForNextLevel;
            if (percentage > 2)
            {
                // this should not happen.
                return;
            }

            var levelDelta = 0;
            while (village.Experience >= expForNextLevel && village.Level < GameMath.MaxVillageLevel)
            {
                village.Experience -= (long)expForNextLevel;
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

using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Net;
using RavenNest.BusinessLogic.Providers;
using RavenNest.DataModels;
using System;

namespace RavenNest.BusinessLogic.Game.Processors.Tasks
{
    public class VillageProcessor : PlayerTaskProcessor
    {
        private TimeSpan updateInterval = TimeSpan.FromSeconds(20);
        private TimeSpan updateExpInterval = TimeSpan.FromSeconds(10);
        private DateTime lastUpdate = DateTime.MinValue;
        private DateTime lastExpSend = DateTime.MinValue;

        public override void Process(
            IIntegrityChecker integrityChecker,
            IGameData gameData,
            IPlayerInventoryProvider inventoryProvider,
            DataModels.GameSession session,
            Character character,
            CharacterState state)
        {
            if (DateTime.UtcNow - lastUpdate < updateInterval)
                return;

            var village = gameData.GetOrCreateVillageBySession(session);
            var players = gameData.GetSessionCharacters(session);

            village.Experience += players.Count * 20;

            var expForNextLevel = GameMath.ExperienceForLevel(village.Level + 1);
            var levelDelta = 0;
            while (village.Experience >= expForNextLevel)
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
                    Experience = village.Experience,
                    Level = village.Level,
                    LevelDelta = levelDelta,
                    HouseSlots = villageHouses.Count
                };

                gameData.Add(gameData.CreateSessionEvent(GameEventType.VillageLevelUp, session, data));
                lastExpSend = DateTime.UtcNow;
            }

            lastUpdate = DateTime.UtcNow;
        }
    }
}

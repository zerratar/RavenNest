using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Net;
using RavenNest.DataModels;
using System;

namespace RavenNest.BusinessLogic.Game.Processors.Tasks
{
    public class VillageProcessor : PlayerTaskProcessor
    {
        private readonly TimeSpan updateInterval = TimeSpan.FromSeconds(30);
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
            var elapsed = DateTime.UtcNow - lastUpdate;
            if (players.Count == 0 && elapsed < updateInterval || !GameData.VillageExpMigrationCompleted)
            {
                return;
            }

            var village = gameData.GetOrCreateVillageBySession(session);

            village.Experience += (long)GameMath.GetVillageExperience(village.Level, players.Count, elapsed);
            var expForNextLevel = GameMath.ExperienceForLevel(village.Level + 1);

            if (village.Experience >= expForNextLevel && (village.Level + 1 > GameMath.MaxLevel))
            {
                village.Experience = (long)expForNextLevel - 1L;
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
                    Experience = village.Experience,
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

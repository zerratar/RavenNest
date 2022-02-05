using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Net;
using System;
using System.Linq;

namespace RavenNest.BusinessLogic.Game
{
    public class VillageManager : IVillageManager
    {
        private readonly IGameData gameData;
        public VillageManager(IGameData gameData)
        {
            this.gameData = gameData;
        }

        public bool AssignPlayerToHouse(Guid sessionId, int slot, Guid characterId)
        {
            var session = gameData.GetSession(sessionId);
            var village = gameData.GetOrCreateVillageBySession(session);
            var houses = gameData.GetOrCreateVillageHouses(village);

            var targetHouse = houses.FirstOrDefault(x => x.Slot == slot);
            if (targetHouse == null)
                return false;

            var player = gameData.GetCharacter(characterId);
            if (player == null)
                return false;

            var existingHouse = houses.FirstOrDefault(x => x.UserId == player.UserId);
            if (existingHouse != null)
                existingHouse.UserId = null;

            targetHouse.UserId = player.UserId;
            return true;
        }

        public bool AssignPlayerToHouse(Guid sessionId, int slot, string userId)
        {
            var session = gameData.GetSession(sessionId);
            var village = gameData.GetOrCreateVillageBySession(session);
            var houses = gameData.GetOrCreateVillageHouses(village);

            var targetHouse = houses.FirstOrDefault(x => x.Slot == slot);
            if (targetHouse == null)
                return false;

            var player = gameData.GetCharacterBySession(session.Id, userId);
            if (player == null)
                return false;

            var existingHouse = houses.FirstOrDefault(x => x.UserId == player.UserId);
            if (existingHouse != null)
                existingHouse.UserId = null;

            targetHouse.UserId = player.UserId;
            return true;
        }

        public bool BuildHouse(Guid sessionId, int slot, int type)
        {
            var session = gameData.GetSession(sessionId);
            var village = gameData.GetOrCreateVillageBySession(session);
            var houses = gameData.GetOrCreateVillageHouses(village);

            var targetHouse = houses.FirstOrDefault(x => x.Slot == slot);
            if (targetHouse == null)
                return false;

            targetHouse.Type = type;
            return true;
        }

        public VillageInfo GetVillageInfo(Guid sessionId)
        {
            var session = gameData.GetSession(sessionId);
            if (session == null) return null;
            var village = gameData.GetOrCreateVillageBySession(session);
            var villageHouses = gameData.GetOrCreateVillageHouses(village);
            return new VillageInfo
            {
                Name = village.Name,
                Level = village.Level,
                Experience = village.Experience,
                Houses = villageHouses.Select(x =>
                   new VillageHouseInfo
                   {
                       Owner = x.UserId != null
                           ? gameData.GetUser(x.UserId.Value).UserId
                           : null,
                       Slot = x.Slot,
                       Type = x.Type
                   }
                 ).ToList()
            };
        }

        public bool RemoveHouse(Guid sessionId, int slot)
        {
            var session = gameData.GetSession(sessionId);
            var village = gameData.GetOrCreateVillageBySession(session);
            var houses = gameData.GetOrCreateVillageHouses(village);

            var targetHouse = houses.FirstOrDefault(x => x.Slot == slot);
            if (targetHouse == null)
                return false;

            targetHouse.Type = -1;
            targetHouse.UserId = null;
            return true;
        }
    }
}

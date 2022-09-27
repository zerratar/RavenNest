using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Net;
using RavenNest.DataModels;
using RavenNest.Models;
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

        public bool SetAllHouses(Guid sessionId, int type)
        {
            var session = gameData.GetSession(sessionId);
            if (session == null) return false;

            var village = gameData.GetOrCreateVillageBySession(session);
            var houses = gameData.GetOrCreateVillageHouses(village);

            foreach (var house in houses)
            {
                house.Type = type;
            }
            return true;
        }

        public bool AssignVillage(Guid sessionId, int type, Guid[] characterIds)
        {
            var session = gameData.GetSession(sessionId);
            if (session == null) return false;

            var village = gameData.GetOrCreateVillageBySession(session);
            var houses = gameData.GetOrCreateVillageHouses(village);

            var i = 0;
            foreach (var house in houses)
            {
                house.Type = type;
                if (i < characterIds.Length)
                {
                    Guid? userId = null;
                    var user = gameData.GetUser(characterIds[i]);
                    if (user != null) userId = user.Id;
                    if (userId == null) userId = gameData.GetCharacter(characterIds[i])?.UserId;
                    house.UserId = userId;
                    i++;
                }
                else
                {
                    house.UserId = null;
                }
            }
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
            return GetVillageInfo(session);
        }

        public VillageInfo GetVillageInfo(DataModels.GameSession session)
        {
            if (session == null) return null;
            var village = gameData.GetOrCreateVillageBySession(session);
            var villageHouses = gameData.GetOrCreateVillageHouses(village);

            var state = gameData.GetSessionState(session.Id);
            var villageLevel = Math.Min(village.Level, GameVersion.IsLessThanOrEquals(state.ClientVersion, "0.8.0.0a") ? 170 : GameMath.MaxVillageLevel);

            return new VillageInfo
            {
                Name = village.Name,
                Level = villageLevel,
                Experience = village.Experience,
                Houses = villageHouses.AsList(x =>
                {
                    RavenNest.DataModels.User owner = null;
                    var uid = x.UserId;
                    if (uid != null)
                    {
                        owner = gameData.GetUser(uid.Value);
                    }
                    return new VillageHouseInfo
                    {
                        Owner = owner?.UserId,
                        Slot = x.Slot,
                        Type = x.Type
                    };
                })
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

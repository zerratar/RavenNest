using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Net;
using RavenNest.DataModels;
using RavenNest.Models;
using System;
using System.Linq;

namespace RavenNest.BusinessLogic.Game
{
    public class VillageManager
    {
        private readonly GameData gameData;
        public VillageManager(GameData gameData)
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
            var beforeUpgrade = GameVersion.IsLessThanOrEquals(state.ClientVersion, "0.8.0.0a");
            var villageLevel = Math.Min(village.Level, beforeUpgrade ? 170 : GameMath.MaxVillageLevel);

            // since we can only have limited amount of houses.
            // we have to ensure we don't go beyond certain limit for the different versions of the game clients.

            var maxHouseCount = beforeUpgrade ? 17 : (GameMath.MaxVillageLevel / 10);

            return new VillageInfo
            {
                Name = village.Name,
                Level = villageLevel,
                Experience = village.Experience,
                Houses = villageHouses
                    .OrderBy(x => x.Slot)
                    .Take(maxHouseCount)
                    .AsList(x =>
                    {
                        RavenNest.DataModels.User owner = null;
                        var uid = x.UserId;
                        if (uid != null)
                        {
                            owner = gameData.GetUser(uid.Value);
                        }

                        // really bad, but for now we have no choice until game client been updated.
                        var twitchUserId = owner != null ? gameData.GetUserAccess(owner.Id, "twitch")?.PlatformId : String.Empty;

                        return new VillageHouseInfo
                        {
                            Owner = twitchUserId,
                            OwnerCharacterId = x.CharacterId,
                            OwnerUserId = x.UserId,
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

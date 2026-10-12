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
            if (village == null) return null;
            var villageHouses = gameData.GetOrCreateVillageHouses(village);

            var state = gameData.GetSessionState(session.Id);
            var villageLevel = Math.Min(village.Level, GameMath.MaxVillageLevel);

            // since we can only have limited amount of houses.
            // we have to ensure we don't go beyond certain limit for the different versions of the game clients.

            var maxHouseCount = GameMath.MaxVillageLevel / 10;
            var resources = gameData.GetResources(village.ResourcesId);

            if (resources == null)
            {
                resources = new DataModels.Resources
                {
                    Id = Guid.NewGuid(),
                };
                gameData.Add(resources);
                village.ResourcesId = resources.Id;
            }

            return new VillageInfo
            {
                Name = village.Name,
                Level = villageLevel,
                Experience = (long)village.Experience,

                Coins = (long)resources.Coins,
                Fish = (long)resources.Fish,
                Wood = (long)resources.Wood,
                Ore = (long)resources.Ore,
                Wheat = (long)resources.Wheat,

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
        /// <summary>
        /// Furthest a building may be placed from the world origin on either axis. The server does
        /// not know the village's layout, so this only stops nonsense values; the game client checks
        /// the real rules (inside the village, on ground, not overlapping) before it sends a move.
        /// </summary>
        private const double MaxPlacementCoordinate = 10000;

        /// <summary>
        /// Positions of the houses that have been moved in build mode. Houses missing from the list
        /// are at their original plot positions.
        /// </summary>
        public VillageHousePlacement[] GetPlacements(Guid sessionId)
        {
            var session = gameData.GetSession(sessionId);
            if (session == null) return Array.Empty<VillageHousePlacement>();

            var village = gameData.GetOrCreateVillageBySession(session);
            if (village == null) return Array.Empty<VillageHousePlacement>();

            return gameData.GetOrCreateVillageHouses(village)
                .Where(x => x.PositionX != null && x.PositionZ != null)
                .OrderBy(x => x.Slot)
                .Select(x => new VillageHousePlacement
                {
                    Slot = x.Slot,
                    X = x.PositionX.Value,
                    Z = x.PositionZ.Value,
                    Rotation = x.Rotation ?? 0
                })
                .ToArray();
        }

        /// <summary>Moves and rotates a house. Its type and resident are unchanged.</summary>
        public bool PlaceHouse(Guid sessionId, int slot, double x, double z, double rotation)
        {
            if (!IsUsable(x) || !IsUsable(z) || double.IsNaN(rotation) || double.IsInfinity(rotation))
            {
                return false;
            }

            var session = gameData.GetSession(sessionId);
            if (session == null) return false;

            var village = gameData.GetOrCreateVillageBySession(session);
            if (village == null) return false;

            var targetHouse = gameData.GetOrCreateVillageHouses(village).FirstOrDefault(h => h.Slot == slot);
            if (targetHouse == null)
                return false;

            rotation %= 360;
            if (rotation < 0) rotation += 360;

            targetHouse.PositionX = x;
            targetHouse.PositionZ = z;
            targetHouse.Rotation = rotation;
            return true;
        }

        /// <summary>Puts a house back on its original plot position.</summary>
        public bool ResetHousePlacement(Guid sessionId, int slot)
        {
            var session = gameData.GetSession(sessionId);
            if (session == null) return false;

            var village = gameData.GetOrCreateVillageBySession(session);
            if (village == null) return false;

            var targetHouse = gameData.GetOrCreateVillageHouses(village).FirstOrDefault(h => h.Slot == slot);
            if (targetHouse == null)
                return false;

            targetHouse.PositionX = null;
            targetHouse.PositionZ = null;
            targetHouse.Rotation = null;
            return true;
        }

        /// <summary>Where the town hall stands. Null X and Z mean the plaza in the game scene.</summary>
        public VillageTownHall GetTownHall(Guid sessionId)
        {
            var session = gameData.GetSession(sessionId);
            if (session == null) return null;

            var village = gameData.GetOrCreateVillageBySession(session);
            if (village == null) return null;

            return new VillageTownHall
            {
                X = village.TownHallX,
                Z = village.TownHallZ,
                Rotation = village.TownHallRotation ?? 0
            };
        }

        /// <summary>
        /// Moves and turns the town hall. As with houses, the game client checks the layout rules
        /// (inside the village, clear of the plots, on ground); the server only refuses unusable numbers.
        /// </summary>
        public bool PlaceTownHall(Guid sessionId, double x, double z, double rotation)
        {
            if (!IsUsable(x) || !IsUsable(z) || double.IsNaN(rotation) || double.IsInfinity(rotation))
            {
                return false;
            }

            var session = gameData.GetSession(sessionId);
            if (session == null) return false;

            var village = gameData.GetOrCreateVillageBySession(session);
            if (village == null) return false;

            rotation %= 360;
            if (rotation < 0) rotation += 360;

            village.TownHallX = x;
            village.TownHallZ = z;
            village.TownHallRotation = rotation;
            return true;
        }

        /// <summary>Puts the town hall back on the plaza.</summary>
        public bool ResetTownHallPlacement(Guid sessionId)
        {
            var session = gameData.GetSession(sessionId);
            if (session == null) return false;

            var village = gameData.GetOrCreateVillageBySession(session);
            if (village == null) return false;

            village.TownHallX = null;
            village.TownHallZ = null;
            village.TownHallRotation = null;
            return true;
        }

        private static bool IsUsable(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value) && Math.Abs(value) <= MaxPlacementCoordinate;
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

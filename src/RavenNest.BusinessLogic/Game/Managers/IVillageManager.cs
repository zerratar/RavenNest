using RavenNest.BusinessLogic.Net;
using System;

namespace RavenNest.BusinessLogic.Game
{
    public interface IVillageManager
    {
        bool AssignPlayerToHouse(Guid sessionId, int slot, string userId);
        bool AssignPlayerToHouse(Guid sessionId, int slot, Guid characterId);
        bool AssignVillage(Guid sessionId, int type, Guid[] userIds);
        bool BuildHouse(Guid sessionId, int slot, int type);
        bool RemoveHouse(Guid sessionId, int slot);
        VillageInfo GetVillageInfo(Guid sessionId);
    }
}

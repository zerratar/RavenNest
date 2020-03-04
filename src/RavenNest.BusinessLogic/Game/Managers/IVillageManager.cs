﻿using System;

namespace RavenNest.BusinessLogic.Game
{
    public interface IVillageManager
    {
        bool AssignPlayerToHouse(Guid sessionId, int slot, string userId);
        bool BuildHouse(Guid sessionId, int slot, int type);
        bool RemoveHouse(Guid sessionId, int slot);
    }
}
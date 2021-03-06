﻿using RavenNest.Models;
using System;
using System.Threading.Tasks;

namespace RavenNest.BusinessLogic.Game
{
    public interface IAdminManager
    {
        PagedPlayerCollection GetPlayersPaged(int offset, int size, string sortOrder, string query);
        PagedSessionCollection GetSessionsPaged(int offset, int size, string sortOrder, string query);
        bool SetCraftingRequirements(string itemQuery, string requirementQuery);
        bool MergePlayerAccounts(string userId);
        bool FixCharacterIndices(string userId);
        bool UpdatePlayerName(Guid characterId, string name);
        bool UpdatePlayerSkill(Guid characterId, string skill, decimal experience);
        bool KickPlayer(Guid characterId);
        bool SuspendPlayer(string userId);
        bool ResetUserPassword(string userid);
        bool ProcessItemRecovery(string query, string identifier);
        bool NerfItems();
        bool RefreshVillageInfo();
        bool RefreshPermissions();
        bool FixLoyaltyPoints();
        //bool FixCharacterExpGain(Guid characterId);
    }
}

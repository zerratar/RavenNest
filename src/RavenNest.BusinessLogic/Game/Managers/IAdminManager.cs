using RavenNest.Models;
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
        bool UpdatePlayerName(string userId, string name, string identifier);
        bool UpdatePlayerSkill(string userId, string skill, decimal experience, string identifier);
        bool KickPlayer(string userId, string identifier);
        bool SuspendPlayer(string userId, string identifier);
        bool ResetUserPassword(string userid);
        bool ProcessItemRecovery(string query, string identifier);
        bool NerfItems();
        bool RefreshVillageInfo();
        Task<bool> RefreshPermissionsAsync();
        //bool FixCharacterExpGain(Guid characterId);
    }
}

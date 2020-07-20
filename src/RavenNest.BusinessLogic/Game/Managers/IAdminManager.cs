using RavenNest.Models;

namespace RavenNest.BusinessLogic.Game
{
    public interface IAdminManager
    {
        PagedPlayerCollection GetPlayersPaged(int offset, int size, string sortOrder, string query);
        PagedSessionCollection GetSessionsPaged(int offset, int size, string sortOrder, string query);
        bool MergePlayerAccounts(string userId);
        bool UpdatePlayerName(string userId, string name);
        bool UpdatePlayerSkill(string userId, string skill, decimal experience);
        bool KickPlayer(string userId);
        bool SuspendPlayer(string userId);
        bool ResetUserPassword(string userid);
    }
}
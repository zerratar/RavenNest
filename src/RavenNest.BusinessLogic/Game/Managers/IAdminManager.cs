using RavenNest.Models;
using System.Threading.Tasks;

namespace RavenNest.BusinessLogic.Game
{
    public interface IAdminManager
    {
        PagedPlayerCollection GetPlayersPaged(int offset, int size, string sortOrder, string query);
        PagedSessionCollection GetSessionsPaged(int offset, int size, string sortOrder, string query);
        bool UpdatePlayerName(string userid, string name);
        bool UpdatePlayerSkill(string userId, string skill, decimal experience);
    }
}
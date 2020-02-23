using RavenNest.Models;
using System.Linq;

namespace RavenNest.BusinessLogic.Game
{
    public class AdminManager : IAdminManager
    {
        private readonly IPlayerManager playerManager;

        public AdminManager(IPlayerManager playerManager)
        {
            this.playerManager = playerManager;
        }

        public PagedPlayerCollection GetPlayersPaged(int offset, int size)
        {
            var allPlayers = this.playerManager.GetPlayers();
            return new PagedPlayerCollection()
            {
                TotalSize = allPlayers.Count,
                Players = allPlayers.Skip(offset).Take(size).ToList()
            };
        }
    }
}
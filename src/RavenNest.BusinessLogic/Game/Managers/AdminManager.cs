using RavenNest.BusinessLogic.Data;
using RavenNest.Models;
using System.Linq;

namespace RavenNest.BusinessLogic.Game
{
    public class AdminManager : IAdminManager
    {
        private readonly IPlayerManager playerManager;
        private readonly IGameData gameData;

        public AdminManager(
            IPlayerManager playerManager, 
            IGameData gameData)
        {
            this.playerManager = playerManager;
            this.gameData = gameData;
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
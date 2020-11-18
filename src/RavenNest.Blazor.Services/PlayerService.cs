using Microsoft.AspNetCore.Http;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Extended;
using RavenNest.BusinessLogic.Game;
using RavenNest.Models;
using RavenNest.Sessions;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RavenNest.Blazor.Services
{
    public class PlayerService : RavenNestService
    {
        private readonly IGameData gameData;
        private readonly IPlayerManager playerManager;

        public PlayerService(
            IGameData gameData,
            IPlayerManager playerManager,
            IHttpContextAccessor accessor,
            ISessionInfoProvider sessionInfoProvider)
            : base(accessor, sessionInfoProvider)
        {
            this.gameData = gameData;
            this.playerManager = playerManager;
        }
        
        public int GetCombatLevel(WebsitePlayer player)
        {
            return (int)(((player.Skills.AttackLevel + player.Skills.DefenseLevel + player.Skills.HealthLevel + player.Skills.StrengthLevel) / 4f) +
                   ((player.Skills.RangedLevel + player.Skills.MagicLevel) / 8f));
        }
        public int GetCombatLevel(Player player)
        {
            return (int)(((player.Skills.AttackLevel + player.Skills.DefenseLevel + player.Skills.HealthLevel + player.Skills.StrengthLevel) / 4f) +
                   ((player.Skills.RangedLevel + player.Skills.MagicLevel) / 8f));
        }

        public async Task<WebsitePlayer> GetMyPlayerByIndexAsync(int index)
        {
            return await Task.Run(() =>
            {
                var session = GetSession();
                var userId = session.UserId;
                return playerManager.GetWebsitePlayer(userId, index.ToString());
            });
        }

        public async Task<IReadOnlyList<WebsitePlayer>> GetMyPlayersAsync()
        {
            return await Task.Run(() =>
            {
                var session = GetSession();
                var userId = session.UserId;
                return playerManager.GetWebsitePlayers(userId);
            });
        }

        public async Task<IReadOnlyList<Player>> SearchForPlayersAsync(string searchText, bool ignoreClanInvitedPlayers = true)
        {
            return await Task.Run(() =>
            {
                var players = playerManager.GetPlayers();
                if (ignoreClanInvitedPlayers)
                {
                    var session = GetSession();
                    var user = gameData.GetUser(session.UserId);
                    if (user == null)
                        return new List<Player>();

                    var clan = gameData.GetClanByUser(user.Id);
                    players = players.Where(x => x.Clan == null &&
                        gameData.GetClanInvitesByCharacter(x.Id).All(y => y.ClanId != clan.Id))
                    .ToList();
                }

                if (string.IsNullOrEmpty(searchText))
                {
                    return new List<Player>();
                }

                return players.Where(x =>
                    x.UserId.Contains(searchText, System.StringComparison.OrdinalIgnoreCase) ||
                    x.UserName.Contains(searchText, System.StringComparison.OrdinalIgnoreCase) ||
                    x.Name.Contains(searchText, System.StringComparison.OrdinalIgnoreCase)
                ).ToList();
            });
        }
    }
}

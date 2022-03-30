using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using RavenNest.BusinessLogic;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Extensions;
using RavenNest.BusinessLogic.Game;
using RavenNest.DataModels;
using RavenNest.Sessions;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RavenNest.Blazor.Services
{
    public class SessionService : RavenNestService
    {
        private readonly IAuthManager authManager;
        private readonly IGameData gameData;
        private readonly AppSettings settings;

        public SessionService(
            IOptions<AppSettings> settings,
            IAuthManager authManager,
            IGameData gameData,
            IHttpContextAccessor accessor,
            ISessionInfoProvider sessionInfoProvider)
            : base(accessor, sessionInfoProvider)
        {
            this.authManager = authManager;
            this.gameData = gameData;
            this.settings = settings.Value;
        }

        public Task<IReadOnlyList<RavenNest.Models.GameSession>> GetGameSessionsAsync()
        {
            return Task.Run(() =>
            {
                var activeSessions = gameData.GetActiveSessions();
                return activeSessions.Select(s => ModelMapper.Map(gameData, s)).AsReadOnlyList();
            });
        }
    }
}

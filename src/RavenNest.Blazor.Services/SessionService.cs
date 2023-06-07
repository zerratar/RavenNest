using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using RavenNest.BusinessLogic;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Extended;
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
        private readonly GameData gameData;
        private readonly SessionManager sessionManager;
        private readonly AppSettings settings;

        public SessionService(
            IOptions<AppSettings> settings,
            IAuthManager authManager,
            GameData gameData,
            IHttpContextAccessor accessor,
            SessionManager sessionManager,
            SessionInfoProvider sessionInfoProvider)
            : base(accessor, sessionInfoProvider)
        {
            this.authManager = authManager;
            this.gameData = gameData;
            this.sessionManager = sessionManager;
            this.settings = settings.Value;
        }

        public void InitiateRaid(RavenNest.Models.GameSession source)
        {
            var s = this.GetSession();
            var raiderGameSession = gameData.GetSession(source.Id);
            var targetSession = gameData.GetSessionByUserId(s.UserId);
            sessionManager.EndSessionAndRaid(raiderGameSession, targetSession, false);
        }

        public void InitiateRaidWar(RavenNest.Models.GameSession source)
        {
            var s = this.GetSession();
            var raiderGameSession = gameData.GetSession(source.Id);
            var targetSession = gameData.GetSessionByUserId(s.UserId);
            sessionManager.EndSessionAndRaid(raiderGameSession, targetSession, true);
        }

        public Task<IReadOnlyList<RavenNest.Models.GameSession>> GetGameSessionsAsync(bool activeSessionsOnly)
        {
            return Task.Run(() =>
            {
                var activeSessions = activeSessionsOnly ? gameData.GetActiveSessions() : gameData.GetLatestSessions();
                return activeSessions.Select(s => ModelMapper.Map(gameData, s)).AsReadOnlyList();
            });
        }
    }
}

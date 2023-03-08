using Microsoft.AspNetCore.Http;
using RavenNest.Blazor.Services.Models;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Game;
using RavenNest.Sessions;
using System.Threading.Tasks;

namespace RavenNest.Blazor.Services
{
    public class AccountService : RavenNestService
    {
        private readonly GameData gameData;
        private readonly ISecureHasher hasher;
        public AccountService(
            GameData gameData,
            ISecureHasher hasher,
            IHttpContextAccessor accessor,
            SessionInfoProvider sessionInfoProvider)
         : base(accessor, sessionInfoProvider)
        {
            this.gameData = gameData;
            this.hasher = hasher;
        }

        public async Task<bool> SetPasswordAsync(CreatePassword model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.Password))
                return false;

            var sessionId = Context.GetSessionId();
            if (!sessionInfoProvider.TryGet(sessionId, out var session))
                return false;

            var user = gameData.GetUser(session.UserId);
            if (user == null)
                return false;

            session.RequiresPasswordChange = false;
            user.PasswordHash = hasher.Get(model.Password);
            return true;
        }

    }
}

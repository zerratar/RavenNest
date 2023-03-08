using Microsoft.AspNetCore.Http;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Game;
using RavenNest.Sessions;
using System;

namespace RavenNest.Blazor.Services
{
    public class CookieService : RavenNestService
    {
        public CookieService(
            IHttpContextAccessor accessor,
            SessionInfoProvider sessionInfoProvider)
            : base(accessor, sessionInfoProvider)
        {
        }

        public bool AcceptedDisclaimer()
        {
            var session = this.GetSession();
            return session != null && session.AcceptedCookiesDisclaimer;
        }

        public void AcceptDisclaimer()
        {
            var session = this.GetSession();
            sessionInfoProvider.SetCookieDisclaimer(session, true);
        }
    }
}

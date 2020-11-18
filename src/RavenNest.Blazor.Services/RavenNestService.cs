using Microsoft.AspNetCore.Http;
using RavenNest.Sessions;

namespace RavenNest.Blazor.Services
{
    public abstract class RavenNestService
    {
        protected readonly IHttpContextAccessor accessor;
        protected readonly ISessionInfoProvider sessionInfoProvider;

        protected RavenNestService(
            IHttpContextAccessor accessor,
            ISessionInfoProvider sessionInfoProvider)
        {
            this.accessor = accessor;
            this.sessionInfoProvider = sessionInfoProvider;
        }

        public HttpContext Context => accessor.HttpContext;
        public ISession Session => accessor.HttpContext.Session;

        public SessionInfo GetSession()
        {
            var id = SessionCookie.GetSessionId(Context);
            if (!this.sessionInfoProvider.TryGet(id, out var sessionInfo))
                sessionInfo = new SessionInfo();
            return sessionInfo;
        }
    }
}

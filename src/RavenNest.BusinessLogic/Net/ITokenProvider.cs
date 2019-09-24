using RavenNest.Models;

namespace RavenNest.BusinessLogic.Net
{
    public interface ITokenProvider
    {
        void SetAuthToken(AuthToken token);
        void SetSessionToken(SessionToken token);
        RavenNest.Models.AuthToken GetAuthToken();
        RavenNest.Models.SessionToken GetSessionToken();
    }
}
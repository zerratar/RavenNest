using RavenNest.Models;

namespace RavenNest.BusinessLogic.Net
{
    public class TokenProvider : ITokenProvider
    {
        private AuthToken authToken;
        private SessionToken sessionToken;

        public AuthToken GetAuthToken() => authToken;

        public SessionToken GetSessionToken() => sessionToken;

        public void SetAuthToken(AuthToken token)
        {
            this.authToken = token;
        }

        public void SetSessionToken(SessionToken token)
        {
            this.sessionToken = token;
        }
    }
}
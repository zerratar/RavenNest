using RavenNest.Models;
using System.Threading.Tasks;

namespace RavenNest.TestClient.Rest
{

    internal class WebBasedAuthEndpoint : IAuthEndpoint
    {
        private readonly ILogger logger;
        private readonly IApiRequestBuilderProvider request;

        public WebBasedAuthEndpoint(ILogger logger, IApiRequestBuilderProvider request)
        {
            this.logger = logger;
            this.request = request;
        }

        public Task<AuthToken> AuthenticateAsync(string username, string password)
        {
            return request.Create()
                .AddParameter("Username", username)
                .AddParameter("Password", password)
                .Build()
                .SendAsync<AuthToken>(ApiRequestTarget.Auth, ApiRequestType.Post);
        }
    }
}

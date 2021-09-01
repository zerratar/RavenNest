using Microsoft.Extensions.Logging;
using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace RavenNest.BusinessLogic
{
    public class RavenBotApiClient : IRavenBotApiClient
    {
        private const string host = "ravenbot.ravenfall.stream";
        private const int port = 6767;

        private readonly ILogger<RavenBotApiClient> logger;

        public RavenBotApiClient(ILogger<RavenBotApiClient> logger)
        {
            this.logger = logger;
        }

        public async Task SendPubSubAccessTokenAsync(string id, string login, string accessToken)
        {
            await SendAsync("pubsub", id, login, accessToken);
        }

        public async Task SendUserRoleAsync(string userId, string userName, string role)
        {
            await SendAsync("userrole", userId, userName, role);
        }

        private async Task SendAsync(string method, params string[] args)
        {
            try
            {
                using (var req = RavenBotRequest.Create(BuildRequestUri(method)))
                {
                    await req.SendAsync(args);
                }
            }
            catch (Exception exc)
            {
                logger.LogError(exc.ToString());
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private string BuildRequestUri(string method)
        {
            return $"{host}:{port}/{method}";
        }

    }
}

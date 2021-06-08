using System.Threading.Tasks;

namespace RavenNest.SDK.Endpoints
{
    internal class WebBasedAdminEndpoint : IAdminEndpoint
    {
        private readonly IApiRequestBuilderProvider request;
        private readonly IRavenNestClient client;
        private readonly ILogger logger;
        public WebBasedAdminEndpoint(
            IRavenNestClient client,
            ILogger logger,
            IApiRequestBuilderProvider request)
        {
            this.client = client;
            this.logger = logger;
            this.request = request;
        }

        public Task<byte[]> DownloadBackupAsync()
        {
            return request.Create()
                .Identifier("download")
                .Method("backup")
                .Build()
                .SendAsync<byte[]>(ApiRequestTarget.Admin, ApiRequestType.Get);
        }
    }
}

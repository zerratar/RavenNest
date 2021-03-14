using RavenNest.Models;

namespace RavenNest.SDK.Endpoints
{
    public class WebApiRequestBuilderProvider : IApiRequestBuilderProvider
    {
        private readonly ILogger logger;
        private readonly IAppSettings settings;
        private readonly ITokenProvider tokenProvider;

        public WebApiRequestBuilderProvider(
            ILogger logger,
            IAppSettings settings,
            ITokenProvider tokenProvider)
        {
            this.logger = logger;
            this.settings = settings;
            this.tokenProvider = tokenProvider;
        }

        public IApiRequestBuilder Create()
        {
            return new WebApiRequestBuilder(settings, logger,
                tokenProvider.GetAuthToken(),
                tokenProvider.GetSessionToken());
        }
    }

    public class WebApiRequestParameter : IRequestParameter
    {
        public string Key { get; }
        public string Value { get; }

        public WebApiRequestParameter(string key, string value)
        {
            Value = value;
            Key = key;
        }
    }

    public interface IRequestParameter
    {
        string Key { get; }
        string Value { get; }
    }
}

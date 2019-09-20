using RavenNest.Models;

namespace RavenNest.SDK.Endpoints
{
    public class WebApiRequestBuilderProvider : IApiRequestBuilderProvider
    {
        private readonly IAppSettings settings;
        private SessionToken sessionToken;
        private AuthToken authToken;

        public WebApiRequestBuilderProvider(IAppSettings settings)
        {
            this.settings = settings;
        }

        public void SetAuthToken(AuthToken currentAuthToken)
        {
            this.authToken = currentAuthToken;
        }

        public void SetSessionToken(SessionToken currentSessionToken)
        {
            this.sessionToken = currentSessionToken;
        }

        public IApiRequestBuilder Create()
        {
            return new WebApiRequestBuilder(settings, authToken, sessionToken);
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
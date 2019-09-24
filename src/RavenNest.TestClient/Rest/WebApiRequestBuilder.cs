using Newtonsoft.Json;
using RavenNest.Models;
using System.Collections.Generic;
using System.Net;

namespace RavenNest.TestClient.Rest
{
    public class WebApiRequestBuilder : IApiRequestBuilder
    {
        private readonly CookieContainer sharedCookieContainer = new CookieContainer();
        private readonly List<IRequestParameter> parameters = new List<IRequestParameter>();
        private readonly IAppSettings appSettings;

        private readonly SessionToken sessionToken;
        private readonly AuthToken authToken;

        private string identifier;
        private string method;

        public WebApiRequestBuilder(IAppSettings appSettings, AuthToken authToken, SessionToken sessionToken)
        {
            this.appSettings = appSettings;
            this.authToken = authToken;
            this.sessionToken = sessionToken;
        }

        public IApiRequestBuilder Identifier(string value)
        {
            this.identifier = value;
            return this;
        }

        public IApiRequestBuilder AddParameter(string value)
        {
            parameters.Add(new WebApiRequestParameter(null, value));
            return this;
        }

        public IApiRequestBuilder AddParameter(string key, object value)
        {
            parameters.Add(new WebApiRequestParameter(key, JsonConvert.SerializeObject(value)));
            return this;
        }

        public IApiRequestBuilder Method(string item)
        {
            this.method = item;
            return this;
        }

        public IApiRequest Build()
        {
            return new WebApiRequest(
                sharedCookieContainer,
                authToken,
                sessionToken,
                appSettings,
                identifier,
                method,
                parameters.ToArray());
        }
    }
}

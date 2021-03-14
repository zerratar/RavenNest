using System.Collections.Generic;
using System.Net;
using Newtonsoft.Json;
using RavenNest.Models;

namespace RavenNest.SDK.Endpoints
{
    public class WebApiRequestBuilder : IApiRequestBuilder
    {
        private readonly CookieContainer sharedCookieContainer = new CookieContainer();
        private readonly List<IRequestParameter> parameters = new List<IRequestParameter>();
        private readonly IAppSettings appSettings;
        private readonly ILogger logger;
        private readonly SessionToken sessionToken;
        private readonly AuthToken authToken;

        private string identifier;
        private string method;

        public WebApiRequestBuilder(IAppSettings appSettings, ILogger logger, AuthToken authToken, SessionToken sessionToken)
        {
            this.appSettings = appSettings;
            this.logger = logger;
            this.authToken = authToken;
            this.sessionToken = sessionToken;
        }

        public IApiRequestBuilder Identifier(string value)
        {
            identifier = value;
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
            method = item;
            return this;
        }

        public IApiRequest Build()
        {
            return new WebApiRequest(
                sharedCookieContainer,
                authToken,
                sessionToken,
                logger,
                appSettings,
                identifier,
                method,
                parameters.ToArray());
        }
    }
}

using Newtonsoft.Json;
using RavenNest.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace RavenNest.TestClient.Rest
{

    public interface IApiRequest
    {
        Task<TResult> SendAsync<TResult, TModel>(ApiRequestTarget target, ApiRequestType type, TModel model);
        Task<TResult> SendAsync<TResult>(ApiRequestTarget target, ApiRequestType type);
        Task SendAsync(ApiRequestTarget target, ApiRequestType type);
    }

    public enum ApiRequestType
    {
        Get,
        Post,
        Update,
        Remove,
    }

    public enum ApiRequestTarget
    {
        Game,
        Items,
        Players,
        Auth,
        Marketplace
    }
    public interface IAuthEndpoint
    {
        Task<AuthToken> AuthenticateAsync(string username, string password);
    }


    public interface IApiRequestBuilder
    {
        IApiRequestBuilder Identifier(string value);
        IApiRequestBuilder AddParameter(string value);
        IApiRequestBuilder AddParameter(string key, object value);
        IApiRequestBuilder Method(string item);
        IApiRequest Build();
    }
    public interface IApiRequestBuilderProvider
    {
        IApiRequestBuilder Create();
    }

    public class WebApiRequestBuilderProvider : IApiRequestBuilderProvider
    {
        private readonly IAppSettings settings;
        private readonly ITokenProvider tokenProvider;

        public WebApiRequestBuilderProvider(
            IAppSettings settings,
            ITokenProvider tokenProvider)
        {
            this.settings = settings;
            this.tokenProvider = tokenProvider;
        }

        public IApiRequestBuilder Create()
        {
            return new WebApiRequestBuilder(settings,
                tokenProvider.GetAuthToken(),
                tokenProvider.GetSessionToken());
        }
    }

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

    internal class WebApiRequest : IApiRequest
    {
        private readonly IAppSettings settings;
        private readonly IRequestParameter[] parameters;
        private readonly string identifier;
        private readonly string method;
        private readonly CookieContainer cookieContainer;
        private readonly AuthToken authToken;
        private readonly SessionToken sessionToken;

        public WebApiRequest(
            CookieContainer cookieContainer,
            AuthToken authToken,
            SessionToken sessionToken,
            IAppSettings settings,
            string identifier,
            string method,
            params IRequestParameter[] parameters)
        {
            this.settings = settings;
            this.identifier = identifier;
            this.method = method;
            this.parameters = parameters;
            this.cookieContainer = cookieContainer;
            this.authToken = authToken;
            this.sessionToken = sessionToken;
        }

        public Task<TResult> SendAsync<TResult>(ApiRequestTarget reqTarget, ApiRequestType type)
        {
            return SendAsync<TResult, object>(reqTarget, type, null);
        }

        public Task SendAsync(ApiRequestTarget target, ApiRequestType type)
        {
            return SendAsync<object>(target, type);
        }

        public async Task<TResult> SendAsync<TResult, TModel>(ApiRequestTarget reqTarget, ApiRequestType type, TModel model)
        {
            // string target, string method, 
            var target = GetTargetUrl(reqTarget);
            var request = (HttpWebRequest)WebRequest.CreateDefault(new Uri(target, UriKind.Absolute));
            var requestData = "";
            //request.Accept = "application/json";

            if (reqTarget == ApiRequestTarget.Game)
            {
                request.Timeout = 25000;
            }

            request.Method = GetMethod(type);
            request.CookieContainer = cookieContainer;
            request.ServerCertificateValidationCallback += (sender, certificate, chain, errors) => true;

            if (authToken != null)
            {
                request.Headers["auth-token"] = JsonConvert.SerializeObject(authToken).Base64Encode();
            }

            if (sessionToken != null)
            {
                request.Headers["session-token"] = JsonConvert.SerializeObject(sessionToken).Base64Encode();
            }

            if (parameters != null)
            {
                var named = this.parameters.Where(x => !string.IsNullOrEmpty(x.Key)).ToList();
                if (model != null)
                {
                    foreach (var param in named)
                    {
                        request.Headers[param.Key] = param.Value;
                    }
                }
                else if (named.Count > 0)
                {
                    requestData = "{" + string.Join(",", named.Select(x => "\"" + x.Key + "\": " + x.Value)) + "}";
                }
            }

            if (model != null)
            {
                requestData = JsonConvert.SerializeObject(model);
            }


            if (!string.IsNullOrEmpty(requestData))
            {
                request.ContentType = "application/json";
                request.ContentLength = Encoding.UTF8.GetByteCount(requestData);
                using (var reqStream = await request.GetRequestStreamAsync())
                using (var writer = new StreamWriter(reqStream))
                {
                    await writer.WriteAsync(requestData);
                    await writer.FlushAsync();
                }
            }

            try
            {
                using (var response = await request.GetResponseAsync())
                using (var resStream = response.GetResponseStream())
                using (var reader = new StreamReader(resStream))
                {
                    if (typeof(TResult) == typeof(object))
                    {
                        return default(TResult);
                    }

                    var responseData = await reader.ReadToEndAsync();
                    return JsonConvert.DeserializeObject<TResult>(responseData);
                }
            }
            catch (Exception exc)
            {
                throw exc;
            }
        }

        private string GetMethod(ApiRequestType type)
        {
            switch (type)
            {
                case ApiRequestType.Post: return HttpMethod.Post.Method;
                case ApiRequestType.Update: return HttpMethod.Put.Method;
                case ApiRequestType.Remove: return HttpMethod.Delete.Method;
                default: return HttpMethod.Get.Method;
            }
        }

        private string GetTargetUrl(ApiRequestTarget reqTarget)
        {
            // http(s)://server:1111/api/
            var url = settings.ApiEndpoint;
            if (!url.EndsWith("/")) url += "/";
            url += reqTarget + "/";
            if (!string.IsNullOrEmpty(identifier)) url += $"{identifier}/";
            if (!string.IsNullOrEmpty(method)) url += $"{method}/";
            if (parameters == null) return url;
            var parameterString = string.Join("/", parameters.Where(x => string.IsNullOrEmpty(x.Key)).Select(x => x.Value));
            if (!string.IsNullOrEmpty(parameterString)) url += $"{parameterString}";
            return url;
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

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using RavenNest.Blazor.Services.Extensions;
using RavenNest.BusinessLogic;
using RavenNest.Sessions;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;

namespace RavenNest.Blazor.Services
{
    // https://github.com/weiks/poq-docs/blob/main/README.md
    public class PoQService : RavenNestService
    {
        private AppSettings settings;

        public PoQService(
            IOptions<AppSettings> settings,
            IHttpContextAccessor accessor,
            ISessionInfoProvider sessionInfoProvider)
            : base(accessor, sessionInfoProvider)
        {
            this.settings = settings.Value;
        }

        public string AuthorizeUrl => GetBaseUrl() +
            $"/api/oauth2/authorize" +
            $"?response_type=code" +
            $"&client_id={settings.PoQClientId}" +
            $"&redirect_uri={HttpUtility.UrlEncode(GetRedirectUrl())}" +
            $"&scope={GetScope()}";

        public async Task<PoQAuthToken> RequestAccessTokenAsync(string code)
        {
            var data = await DoPoqRequestAsync("api/oauth2/token", new PoQAuthRequest
            {
                code = code,
                client_id = settings.PoQClientId,
                client_secret = settings.PoQClientSecret,
                redirect_uri = GetRedirectUrl()
            }, null);

            return Newtonsoft.Json.JsonConvert.DeserializeObject<PoQAuthToken>(data);
        }

        public Task<string> DoPoqRequestAsync(string url, PoQAuthToken token = null)
        {
            return DoPoqRequestAsync(url, null, token);
        }

        public async Task<string> DoPoqRequestAsync(string url, object model, PoQAuthToken token)
        {
            try
            {
                url = BuildRequestUrl(url);

                using (var client = new HttpClient())
                {
                    if (token != null && !string.IsNullOrEmpty(token.access_token))
                    {
                        client.DefaultRequestHeaders.Add("Authorization", "Bearer " + token.access_token);
                    }

                    using var res = model != null
                        ? (await client.PostAsync(url, new FormUrlEncodedContent(model.ToKeyValue())))
                        : (await client.GetAsync(url));

                    //res.EnsureSuccessStatusCode();
                    return await res.Content.ReadAsStringAsync();
                }
            }
            catch
            {
                return null;
            }
        }

        private string BuildRequestUrl(string url)
        {
            if (!url.StartsWith("http"))
            {
                if (!url.StartsWith("/"))
                {
                    url = "/" + url;
                }
                url = GetBaseUrl() + url;
            }

            return url;
        }

        private string GetScope() => "email";

#if DEBUG
        private string GetBaseUrl() => settings.PoQDevUrl;
#else
        private string GetBaseUrl() => settings.PoQProdUrl;
#endif

        private string GetRedirectUrl()
#if DEBUG
            => $"https://{Context.Request.Host}/poq-auth";
#else
            => "https://www.ravenfall.stream/poq-auth";
#endif

    }
    public class PoQAuthToken
    {
        public string scope { get; set; }
        public string access_token { get; set; }
        public string refresh_token { get; set; }
        public string token_type { get; set; }
        public long expires_in { get; set; }

        //public DateTime Created { get; set; }
        //public bool IsExpired => DateTime.UtcNow >= Created.Add
    }

    public class PoQAuthRequest
    {
        public string code { get; set; }
        public string grant_type { get; set; } = "authorization_code";
        public string client_id { get; set; }
        public string client_secret { get; set; }
        public string redirect_uri { get; set; }
    }
}

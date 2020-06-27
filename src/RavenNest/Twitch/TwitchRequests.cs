using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RavenNest.BusinessLogic;

namespace RavenNest.Twitch
{
    public class TwitchRequests
    {
        private readonly string accessToken;
        private readonly string clientId;
        private readonly string clientSecret;
        private TwitchAuth auth;

        public TwitchRequests(
            string accessToken = null,
            string clientId = null,
            string clientSecret = null)
        {
            this.accessToken = accessToken;
            this.clientId = clientId;
            this.clientSecret = clientSecret;
        }

        public async Task<string> GetUsersAsync()
        {
            if (this.auth == null || this.auth.Expires <= DateTime.UtcNow)
            {
                this.auth = await AuthenticateAsync();
            }

            return await TwitchRequestAsync("https://api.twitch.tv/helix/users", auth.access_token);
        }

        public async Task<TwitchChannel> GetUserAsync(string userId)
        {
            if (this.auth == null || this.auth.Expires <= DateTime.UtcNow)
            {
                this.auth = await AuthenticateAsync();
            }

            return JsonConvert.DeserializeObject<TwitchChannel>(
                await TwitchRequestAsync("https://api.twitch.tv/kraken/channels/" + userId, auth.access_token));
        }

        public async Task<TwitchAuth> AuthenticateAsync()
        {
            var result = await RequestAsync(
                "POST",
                "https://id.twitch.tv/oauth2/token",
                null,
                parameters: new Dictionary<string, string> {
                    { "client_id", clientId },
                    { "client_secret", clientSecret },
                    { "grant_type", "client_credentials" },
                    { "scope", "channel_read" }
                });
            var authData = JsonConvert.DeserializeObject<TwitchAuth>(result);
            authData.Expires = DateTime.UtcNow.Add(TimeSpan.FromSeconds(authData.expires_in - 30));
            return authData;
        }

        private async Task<string> TwitchRequestAsync(string url, string access_token = null, Dictionary<string, string> parameters = null)
        {
            return await RequestAsync("GET", url, access_token, parameters);
        }

        private async Task<string> RequestAsync(string method, string url, string access_token, Dictionary<string, string> parameters = null)
        {

            if (parameters != null)
            {
                url += "?" + string.Join("&", parameters.Select(x => x.Key + "=" + x.Value));
            }

            var req = (HttpWebRequest)HttpWebRequest.Create(url);
            req.Method = method;
            req.Accept = "application/vnd.twitchtv.v5+json";

            if (!string.IsNullOrEmpty(access_token ?? accessToken))
            {
                req.Headers["Authorization"] = $"Bearer {access_token ?? accessToken}";
            }

            if (!string.IsNullOrEmpty(clientId))
            {
                req.Headers["Client-ID"] = clientId;
            }

            try
            {
                using (var res = await req.GetResponseAsync())
                using (var stream = res.GetResponseStream())
                using (var reader = new StreamReader(stream))
                {
                    return await reader.ReadToEndAsync();
                }
            }
            catch (WebException we)
            {
                var resp = we.Response as HttpWebResponse;
                if (resp != null)
                {
                    using (var stream = resp.GetResponseStream())
                    using (var reader = new StreamReader(stream))
                    {
                        var errorText = await reader.ReadToEndAsync();
                        System.IO.File.AppendAllText("request-error.log", errorText);
                    }
                }
                throw;
            }
        }

        public class TwitchAuth
        {
            public DateTime Expires { get; set; }

            public string access_token { get; set; }
            public string refresh_token { get; set; }
            public string token_type { get; set; }
            public int expires_in { get; set; }
            public string[] scope { get; set; }
        }

        public class TwitchUserListResponse
        {
            public List<TwitchUser> Data { get; set; }
        }

        public class TwitchUserData
        {
            public TwitchUser[] Data { get; set; }
        }

        public class TwitchChannel
        {
            public bool mature { get; set; }
            public string status { get; set; }
            public string broadcaster_language { get; set; }
            public string broadcaster_software { get; set; }
            public string display_name { get; set; }
            public string game { get; set; }
            public string language { get; set; }
            public string _id { get; set; }
            public string name { get; set; }
            public DateTime created_at { get; set; }
            public DateTime updated_at { get; set; }
            public bool partner { get; set; }
            public string logo { get; set; }
            public string video_banner { get; set; }
            public string profile_banner { get; set; }
            public object profile_banner_background_color { get; set; }
            public string url { get; set; }
            public int views { get; set; }
            public int followers { get; set; }
            public string broadcaster_type { get; set; }
            public string description { get; set; }
            public bool private_video { get; set; }
            public bool privacy_options_enabled { get; set; }
        }

        public class TwitchUser
        {
            public string Id { get; set; }
            public string Login { get; set; }
            [JsonProperty("display_name")]
            public string DisplayName { get; set; }
            public string Type { get; set; }
            public string Email { get; set; }
        }
    }
}

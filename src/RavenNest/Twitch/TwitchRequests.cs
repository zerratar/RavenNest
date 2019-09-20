using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace RavenNest.Twitch
{
    public class TwitchRequests
    {
        private readonly string accessToken;

        public TwitchRequests(string accessToken)
        {
            this.accessToken = accessToken;
        }
        public async Task<string> GetUsersAsync()
        {
            return await TwitchRequestAsync("https://api.twitch.tv/helix/users", accessToken);
        }
        private async Task<string> TwitchRequestAsync(string url, string accessToken)
        {
            var req = (HttpWebRequest)HttpWebRequest.Create(url);
            req.Method = "GET";
            req.Headers["Authorization"] = $"Bearer {accessToken}";
            using (var res = await req.GetResponseAsync())
            using (var stream = res.GetResponseStream())
            using (var reader = new StreamReader(stream))
            {
                return await reader.ReadToEndAsync();
            }
        }
        public class TwitchUserListResponse
        {
            public List<TwitchUser> Data { get; set; }
        }

        public class TwitchUserData
        {
            public TwitchUser[] Data { get; set; }
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

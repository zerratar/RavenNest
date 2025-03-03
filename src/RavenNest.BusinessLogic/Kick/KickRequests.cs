using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace RavenNest.Kick
{
    public class KickRequests
    {
        private readonly string code;
        private readonly string scope;
        private readonly string codeVerifier;
        private readonly string codeChallenge;
        private readonly string redirectUrl;
        private readonly string accessToken;
        private readonly string clientId;
        private readonly string clientSecret;
        private KickAuth auth;

        private string idUrl = "https://id.kick.com/";
        private string apiUrl = "https://api.kick.com/";

        public KickRequests(
            string code,
            string scope,
            string code_verifier,
            string code_challenge,
            string redirectUrl,
            string clientId,
            string clientSecret)
        {
            this.code = code;
            this.scope = scope;
            this.codeVerifier = code_verifier;
            this.codeChallenge = code_challenge;
            this.redirectUrl = redirectUrl;
            this.clientId = clientId;
            this.clientSecret = clientSecret;
        }

        public KickRequests(string access_token, string clientId, string clientSecret)
        {
            this.accessToken = access_token;
            this.clientId = clientId;
            this.clientSecret = clientSecret;
        }

        public async Task<string> GetUserAsync()
        {
            return await GetAsync(apiUrl + "public/v1/users");
        }

        public Task<KickAuth> AuthenticateAsync()
        {
            return AuthenticateAsync(false);
        }

        public async Task<KickAuth> AuthenticateAsync(bool refreshToken)
        {
            Dictionary<string, string> parameters = new Dictionary<string, string> {
                    { "code", code },
                    { "client_id", clientId },
                    { "client_secret", clientSecret },
                    { "redirect_uri", redirectUrl },
                    { "grant_type", "authorization_code" },
                    { "code_verifier", codeVerifier },
                };

            if (refreshToken)
            {
                parameters["refresh_token"] = this.auth.refresh_token;
            }

            var result = await PostUrlEncodedFormAsync(
                idUrl + "oauth/token",
                parameters);

            var authData = JsonConvert.DeserializeObject<KickAuth>(result);

            authData.Expires = refreshToken
                ? DateTime.UtcNow.Add(TimeSpan.FromMinutes(60))
                : DateTime.UtcNow.Add(TimeSpan.FromSeconds(authData.expires_in - 30));

            return authData;
        }

        private async Task<string> GetAsync(string url)
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", "Bearer " + accessToken);
            try
            {
                var res = await client.GetAsync(url);
                using (var stream = res.Content.ReadAsStream())
                using (var reader = new StreamReader(stream))
                {
                    var result = await reader.ReadToEndAsync();
                    if (res.IsSuccessStatusCode)
                    {
                        return result;
                    }
                    else
                    {
                        System.IO.File.AppendAllText("kick-request-error.log", result);
                        throw new Exception(result);
                    }
                }
            }
            catch (Exception we)
            {
                System.IO.File.AppendAllText("kick-request-error.log", we.ToString());
                throw;
            }
        }

        private async Task<string> PostUrlEncodedFormAsync(
            string url,
            Dictionary<string, string> parameters = null,
            int retryCount = 0)
        {
            if (retryCount >= 3)
            {
                System.IO.File.AppendAllText("kick-request-error.log", "Internal Server Error. Login retried 3 times.");
                throw new Exception("Internal Server Error. Login retried 3 times.");
            }
            using var client = new HttpClient();
            try
            {
                var data = new FormUrlEncodedContent(parameters);
                var res = await client.PostAsync(url, data);
                using (var stream = res.Content.ReadAsStream())
                using (var reader = new StreamReader(stream))
                {
                    var result = await reader.ReadToEndAsync();
                    if (res.IsSuccessStatusCode)
                    {
                        return result;
                    }
                    else
                    {
                        System.IO.File.AppendAllText("kick-request-error.log", result);
                        throw new Exception(result);
                    }
                }
            }
            catch (Exception we)
            {
                System.IO.File.AppendAllText("kick-request-error.log", we.ToString());
                throw;
            }
        }

        public class KickAuth
        {
            public DateTime Expires { get; set; }

            public string access_token { get; set; }
            public string refresh_token { get; set; }
            public string token_type { get; set; }
            public int expires_in { get; set; }
            public string scope { get; set; }
        }

        public class KickValidateResponse
        {
            [JsonProperty("client_id")]
            public string ClientID { get; set; }
            public string Login { get; set; }
            public string[] Scopes { get; set; }
            [JsonProperty("user_id")]
            public string UserID { get; set; }
        }

        public class KickUserListResponse
        {
            [JsonProperty("message")]
            public string Message { get; set; }
            [JsonProperty("data")]
            public List<KickUser> Data { get; set; }
        }

        public class KickUserData
        {
            [JsonProperty("data")]
            public KickUser[] Data { get; set; }
        }

        public class KickChannel
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

        public class KickUser
        {
            [JsonProperty("id")]
            public long Id { get; set; }

            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("profile_picture")]
            public string ProfilePicture { get; set; }

            [JsonProperty("email")]
            public string Email { get; set; }
        }
    }
}

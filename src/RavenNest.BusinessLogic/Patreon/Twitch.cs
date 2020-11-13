using System;
using Newtonsoft.Json;

namespace RavenNest.BusinessLogic.Patreon
{
    public partial class Twitch
    {
        [JsonProperty("scopes")]
        public string[] Scopes { get; set; }

        [JsonProperty("url")]
        public Uri Url { get; set; }

        [JsonProperty("user_id")]
        public string UserId { get; set; }
    }
}


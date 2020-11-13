using System;
using Newtonsoft.Json;

namespace RavenNest.BusinessLogic.Patreon
{
    public partial class Twitter
    {
        [JsonProperty("url")]
        public Uri Url { get; set; }

        [JsonProperty("user_id")]
        [JsonConverter(typeof(ParseStringConverter))]
        public long UserId { get; set; }
    }
}



using Newtonsoft.Json;

namespace RavenNest.BusinessLogic.Patreon
{
    public partial class SocialConnections
    {
        [JsonProperty("deviantart")]
        public object Deviantart { get; set; }

        [JsonProperty("discord")]
        public object Discord { get; set; }

        [JsonProperty("facebook")]
        public object Facebook { get; set; }

        [JsonProperty("google")]
        public object Google { get; set; }

        [JsonProperty("instagram")]
        public object Instagram { get; set; }

        [JsonProperty("reddit")]
        public object Reddit { get; set; }

        [JsonProperty("spotify")]
        public object Spotify { get; set; }

        [JsonProperty("twitch")]
        public Twitch Twitch { get; set; }

        [JsonProperty("twitter")]
        public Twitter Twitter { get; set; }

        [JsonProperty("youtube")]
        public Twitch Youtube { get; set; }
    }
}

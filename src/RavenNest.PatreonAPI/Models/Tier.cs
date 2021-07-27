using Newtonsoft.Json;

namespace RavenNest.PatreonAPI.Models
{
    public class Tier
    {
        [JsonProperty(PropertyName = "type")]
        public string Type { get; set; }

        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }

        [JsonProperty(PropertyName = "attributes")]
        public TierAttributes Attributes { get; set; }
    }
}

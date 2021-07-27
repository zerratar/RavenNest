using Newtonsoft.Json;

namespace RavenNest.PatreonAPI.Models
{
    public class Links
    {
        [JsonProperty(PropertyName = "first")]
        public string First { get; set; }

        [JsonProperty(PropertyName = "next")]
        public string Next { get; set; }

        [JsonProperty(PropertyName = "related")]
        public string Related { get; set; }

        [JsonProperty(PropertyName = "self")]
        public string Self { get; set; }
    }
}

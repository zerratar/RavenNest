using Newtonsoft.Json;

namespace RavenNest.PatreonAPI.Models
{
    public class User
    {
        [JsonProperty(PropertyName = "type")]
        public string Type { get; set; }

        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }

        [JsonProperty(PropertyName = "attributes")]
        public UserAttributes Attributes { get; set; }
    }
}

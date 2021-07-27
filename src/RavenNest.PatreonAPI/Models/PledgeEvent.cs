using Newtonsoft.Json;

namespace RavenNest.PatreonAPI.Models
{
    public class PledgeEvent
    {
        [JsonProperty(PropertyName = "type")]
        public string Type { get; set; }

        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }

        [JsonProperty(PropertyName = "attributes")]
        public PledgeEventAttributes Attributes { get; set; }

        [JsonProperty(PropertyName = "relationships")]
        public PledgeEventRelationships Relationships { get; set; }
    }
}

using Newtonsoft.Json;

namespace RavenNest.PatreonAPI.Models
{
    public class Campaign
    {
        [JsonProperty(PropertyName = "type")]
        public string Type { get; set; }

        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }

        [JsonProperty(PropertyName = "attributes")]
        public CampaignAttributes Attributes { get; set; }

        [JsonProperty(PropertyName = "relationships")]
        public CampaignRelationships Relationships { get; set; }
    }
}

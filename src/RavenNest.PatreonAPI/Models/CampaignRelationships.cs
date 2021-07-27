using Newtonsoft.Json;
using System.Collections.Generic;

namespace RavenNest.PatreonAPI.Models
{
    public class CampaignRelationships
    {
        [JsonProperty(PropertyName = "creator")]
        public User Creator { get; set; }

        [JsonProperty(PropertyName = "tiers")]
        public List<Tier> Tiers { get; set; }
    }
}

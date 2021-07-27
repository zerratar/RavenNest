using Newtonsoft.Json;
using System.Collections.Generic;

namespace RavenNest.PatreonAPI.Models
{
    public class MemberRelationships
    {
        [JsonProperty(PropertyName = "user")]
        public User User { get; set; }

        [JsonProperty(PropertyName = "pledge_history/data")]
        public List<PledgeEvent> PledgeHistory { get; set; }

        [JsonProperty(PropertyName = "currently_entitled_tiers/data")]
        public List<Tier> CurrentlyEntitledTiers { get; set; }
    }
}

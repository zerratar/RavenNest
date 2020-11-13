using Newtonsoft.Json;

namespace RavenNest.BusinessLogic.Patreon
{
    public partial class IncludedRelationships
    {
        [JsonProperty("creator", NullValueHandling = NullValueHandling.Ignore)]
        public Campaign Creator { get; set; }

        [JsonProperty("goals", NullValueHandling = NullValueHandling.Ignore)]
        public Address Goals { get; set; }

        [JsonProperty("rewards", NullValueHandling = NullValueHandling.Ignore)]
        public Address Rewards { get; set; }

        [JsonProperty("campaign", NullValueHandling = NullValueHandling.Ignore)]
        public Campaign Campaign { get; set; }
    }
}

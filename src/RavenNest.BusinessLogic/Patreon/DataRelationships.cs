using Newtonsoft.Json;

namespace RavenNest.BusinessLogic.Patreon
{
    public partial class DataRelationships
    {
        [JsonProperty("address")]
        public Address Address { get; set; }

        [JsonProperty("campaign")]
        public Campaign Campaign { get; set; }

        [JsonProperty("user")]
        public Campaign User { get; set; }
    }
}


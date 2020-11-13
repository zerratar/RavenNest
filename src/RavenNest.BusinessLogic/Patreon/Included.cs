using Newtonsoft.Json;

namespace RavenNest.BusinessLogic.Patreon
{
    public partial class Included
    {
        [JsonProperty("attributes")]
        public IncludedAttributes Attributes { get; set; }

        [JsonProperty("id")]
        [JsonConverter(typeof(ParseStringConverter))]
        public long Id { get; set; }

        [JsonProperty("relationships", NullValueHandling = NullValueHandling.Ignore)]
        public IncludedRelationships Relationships { get; set; }

        [JsonProperty("type")]
        public TypeEnum Type { get; set; }
    }
}


using Newtonsoft.Json;

namespace RavenNest.BusinessLogic.Patreon
{
    public partial class Data
    {
        [JsonProperty("attributes")]
        public DataAttributes Attributes { get; set; }

        [JsonProperty("id")]
        public object Id { get; set; }

        [JsonProperty("relationships")]
        public DataRelationships Relationships { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }
    }
}

using Newtonsoft.Json;

namespace RavenNest.BusinessLogic.Patreon
{
    public partial class Dat
    {
        [JsonProperty("id")]
        [JsonConverter(typeof(ParseStringConverter))]
        public long Id { get; set; }

        [JsonProperty("type")]
        public TypeEnum Type { get; set; }
    }
}


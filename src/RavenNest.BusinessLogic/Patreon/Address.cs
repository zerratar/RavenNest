using Newtonsoft.Json;

namespace RavenNest.BusinessLogic.Patreon
{
    public partial class Address
    {
        [JsonProperty("data")]
        public Dat[] Data { get; set; }
    }
}


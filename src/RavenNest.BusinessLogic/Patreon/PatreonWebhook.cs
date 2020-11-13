using Newtonsoft.Json;

namespace RavenNest.BusinessLogic.Patreon
{
    public partial class PatreonWebhook
    {
        [JsonProperty("data")]
        public Data Data { get; set; }

        [JsonProperty("included")]
        public Included[] Included { get; set; }

        [JsonProperty("links")]
        public PatreonWebhookLinks Links { get; set; }
    }
}

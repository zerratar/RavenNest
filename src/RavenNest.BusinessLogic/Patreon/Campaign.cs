using Newtonsoft.Json;

namespace RavenNest.BusinessLogic.Patreon
{
    public partial class Campaign
    {
        [JsonProperty("data")]
        public Dat Data { get; set; }

        [JsonProperty("links")]
        public CampaignLinks Links { get; set; }
    }
}


using System;
using Newtonsoft.Json;

namespace RavenNest.BusinessLogic.Patreon
{
    public partial class CampaignLinks
    {
        [JsonProperty("related")]
        public Uri Related { get; set; }
    }
}


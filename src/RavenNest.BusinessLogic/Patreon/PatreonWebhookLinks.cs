using System;
using Newtonsoft.Json;

namespace RavenNest.BusinessLogic.Patreon
{
    public partial class PatreonWebhookLinks
    {
        [JsonProperty("self")]
        public Uri Self { get; set; }
    }
}

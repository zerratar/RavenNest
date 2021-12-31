using Newtonsoft.Json;

namespace RavenNest.BusinessLogic.Patreon
{
    public class ZapierPatreonData : IPatreonData
    {
        [JsonProperty("email")]
        public string Email { get; set; }

        [JsonProperty("fullname")]
        public string FullName { get; set; }

        [JsonProperty("tier")]
        public string RewardTitle { get; set; }
        [JsonProperty("patreonId")]
        public long PatreonId { get; set; }
        [JsonProperty("pledgeAmount")]
        public string PledgeAmountCents { get; set; }
        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("twitchId")]
        public string TwitchUserId { get; set; }
        
        [JsonProperty("twitchUrl")]
        public string TwitchUrl { get; set; }
        public int Tier { get; set; }
    }
}

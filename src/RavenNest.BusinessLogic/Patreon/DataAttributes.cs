using System;
using Newtonsoft.Json;

namespace RavenNest.BusinessLogic.Patreon
{
    public partial class DataAttributes
    {
        [JsonProperty("campaign_currency")]
        public string CampaignCurrency { get; set; }

        [JsonProperty("campaign_lifetime_support_cents")]
        public long CampaignLifetimeSupportCents { get; set; }

        [JsonProperty("campaign_pledge_amount_cents")]
        public long CampaignPledgeAmountCents { get; set; }

        [JsonProperty("full_name")]
        public string FullName { get; set; }

        [JsonProperty("is_follower")]
        public bool IsFollower { get; set; }

        [JsonProperty("last_charge_date")]
        public DateTimeOffset LastChargeDate { get; set; }

        [JsonProperty("last_charge_status")]
        public string LastChargeStatus { get; set; }

        [JsonProperty("lifetime_support_cents")]
        public long LifetimeSupportCents { get; set; }

        [JsonProperty("patron_status")]
        public string PatronStatus { get; set; }

        [JsonProperty("pledge_amount_cents")]
        public long PledgeAmountCents { get; set; }

        [JsonProperty("pledge_relationship_start")]
        public DateTimeOffset PledgeRelationshipStart { get; set; }
    }
}

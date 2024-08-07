﻿using Newtonsoft.Json;
using System;

namespace RavenNest.PatreonAPI.Models
{
    public class MemberAttributes
    {
        [JsonProperty(PropertyName = "campaign_lifetime_support_cents")]
        public int? CampaignLifetimeSupportCents { get; set; }

        [JsonProperty(PropertyName = "currently_entitled_amount_cents")]
        public int? EntitledAmountCents { get; set; }

        [JsonProperty(PropertyName = "email")]
        public string Email { get; set; }

        [JsonProperty(PropertyName = "full_name")]
        public string FullName { get; set; }

        [JsonProperty(PropertyName = "is_follower")]
        public bool IsFollower { get; set; }

        [JsonProperty(PropertyName = "last_charge_date")]
        public DateTime? LastChargeDate { get; set; }

        [JsonProperty(PropertyName = "last_charge_status")]
        public string LastChargeStatus { get; set; }

        [JsonProperty(PropertyName = "lifetime_support_cents")]
        public int? LifetimeSupportCents { get; set; }

        [JsonProperty(PropertyName = "next_charge_date")]
        public DateTime? NextChargeDate { get; set; }

        [JsonProperty(PropertyName = "note")]
        public string Note { get; set; }

        [JsonProperty(PropertyName = "patron_status")]
        public string PatreonStatus { get; set; }

        [JsonProperty(PropertyName = "pledge_cadence")]
        public int? PledgeCadence { get; set; }

        [JsonProperty(PropertyName = "pledge_relationship_start")]
        public DateTime? PledgeRelationshipStart { get; set; }

        [JsonProperty(PropertyName = "will_pay_amount_cents")]
        public int? WillPayAmountCents { get; set; }
    }
}

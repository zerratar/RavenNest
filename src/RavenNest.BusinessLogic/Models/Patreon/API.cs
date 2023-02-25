using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace RavenNest.BusinessLogic.Models.Patreon.API
{
    public class PatreonCampaign
    {
        public string Id { get; set; }
        public List<PatreonTier> Tiers { get; set; }
        public long PatreonCount { get; set; }
    }

    public class PatreonTier
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public long AmountCents { get; set; }
        public int Level { get; set; }
    }

    public class PatreonMemberCollection
    {
        public partial class Root
        {
            [JsonProperty("data")]
            public List<PatreonMemberCollection.MemberData> Data { get; set; }

            [JsonProperty("links")]
            public Links Links { get; set; }

            [JsonProperty("meta")]
            public Meta Meta { get; set; }
        }

        public partial class MemberData
        {
            [JsonProperty("attributes")]
            public Attributes Attributes { get; set; }

            [JsonProperty("id")]
            public Guid Id { get; set; }

            [JsonProperty("type")]
            public string Type { get; set; }
        }

        public partial class Attributes
        {
            [JsonProperty("currently_entitled_amount_cents")]
            public long CurrentlyEntitledAmountCents { get; set; }

            [JsonProperty("patron_status")]
            public string PatronStatus { get; set; }
        }

        public partial class Links
        {
            [JsonProperty("next")]
            public string Next { get; set; }
        }

        public partial class Meta
        {
            [JsonProperty("pagination")]
            public Pagination Pagination { get; set; }
        }

        public partial class Pagination
        {
            [JsonProperty("cursors")]
            public Links Cursors { get; set; }

            [JsonProperty("total")]
            public long Total { get; set; }
        }
    }

    public class PatreonCampaigns
    {
        public partial class Root
        {
            [JsonProperty("data")]
            public CampaignData[] Data { get; set; }

            [JsonProperty("included")]
            public Included[] Included { get; set; }

            [JsonProperty("meta")]
            public Meta Meta { get; set; }
        }

        public partial class CampaignData
        {
            [JsonProperty("attributes")]
            public DatumAttributes Attributes { get; set; }

            [JsonProperty("id")]
            public string Id { get; set; }

            [JsonProperty("relationships")]
            public Relationships Relationships { get; set; }

            [JsonProperty("type")]
            public string Type { get; set; }
        }

        public partial class DatumAttributes
        {
            [JsonProperty("created_at")]
            public DateTimeOffset CreatedAt { get; set; }

            [JsonProperty("creation_name")]
            public string CreationName { get; set; }

            [JsonProperty("discord_server_id")]
            public string DiscordServerId { get; set; }

            [JsonProperty("google_analytics_id")]
            public object GoogleAnalyticsId { get; set; }

            [JsonProperty("has_rss")]
            public bool HasRss { get; set; }

            [JsonProperty("has_sent_rss_notify")]
            public bool HasSentRssNotify { get; set; }

            [JsonProperty("image_small_url")]
            public Uri ImageSmallUrl { get; set; }

            [JsonProperty("image_url")]
            public Uri ImageUrl { get; set; }

            [JsonProperty("is_charged_immediately")]
            public bool IsChargedImmediately { get; set; }

            [JsonProperty("is_monthly")]
            public bool IsMonthly { get; set; }

            [JsonProperty("is_nsfw")]
            public bool IsNsfw { get; set; }

            [JsonProperty("main_video_embed")]
            public string MainVideoEmbed { get; set; }

            [JsonProperty("main_video_url")]
            public Uri MainVideoUrl { get; set; }

            [JsonProperty("one_liner")]
            public object OneLiner { get; set; }

            [JsonProperty("patron_count")]
            public long PatronCount { get; set; }

            [JsonProperty("pay_per_name")]
            public string PayPerName { get; set; }

            [JsonProperty("pledge_url")]
            public string PledgeUrl { get; set; }

            [JsonProperty("published_at")]
            public DateTimeOffset PublishedAt { get; set; }

            [JsonProperty("rss_artwork_url")]
            public object RssArtworkUrl { get; set; }

            [JsonProperty("rss_feed_title")]
            public object RssFeedTitle { get; set; }

            [JsonProperty("summary")]
            public string Summary { get; set; }

            [JsonProperty("thanks_embed")]
            public object ThanksEmbed { get; set; }

            [JsonProperty("thanks_msg")]
            public string ThanksMsg { get; set; }

            [JsonProperty("thanks_video_url")]
            public object ThanksVideoUrl { get; set; }
        }

        public partial class Relationships
        {
            [JsonProperty("tiers")]
            public Tiers Tiers { get; set; }
        }

        public partial class Tiers
        {
            [JsonProperty("data")]
            public TiersDatum[] Data { get; set; }
        }

        public partial class TiersDatum
        {
            [JsonProperty("id")]
            public string Id { get; set; }

            [JsonProperty("type")]
            public string Type { get; set; }
        }

        public partial class Included
        {
            [JsonProperty("attributes")]
            public IncludedAttributes Attributes { get; set; }

            [JsonProperty("id")]
            public string Id { get; set; }

            [JsonProperty("type")]
            public string Type { get; set; }
        }

        public partial class IncludedAttributes
        {
            [JsonProperty("amount_cents")]
            public long AmountCents { get; set; }

            [JsonProperty("title")]
            public string Title { get; set; }
        }

        public partial class Meta
        {
            [JsonProperty("pagination")]
            public Pagination Pagination { get; set; }
        }

        public partial class Pagination
        {
            [JsonProperty("cursors")]
            public Cursors Cursors { get; set; }

            [JsonProperty("total")]
            public long Total { get; set; }
        }

        public partial class Cursors
        {
            [JsonProperty("next")]
            public object Next { get; set; }
        }
    }


    public class PatreonIdentity
    {

        public partial class Root
        {
            [JsonProperty("data")]
            public Data Data { get; set; }

            [JsonProperty("included")]
            public Included[] Included { get; set; }

            [JsonProperty("links")]
            public Links Links { get; set; }
        }

        public partial class Data
        {
            [JsonProperty("attributes")]
            public DataAttributes Attributes { get; set; }

            [JsonProperty("id")]
            public string Id { get; set; }

            [JsonProperty("relationships")]
            public DataRelationships Relationships { get; set; }

            [JsonProperty("type")]
            public string Type { get; set; }
        }

        public partial class DataAttributes
        {
            [JsonProperty("created")]
            public DateTimeOffset Created { get; set; }

            [JsonProperty("email")]
            public string Email { get; set; }

            [JsonProperty("first_name")]
            public string FirstName { get; set; }

            [JsonProperty("full_name")]
            public string FullName { get; set; }

            [JsonProperty("image_url")]
            public Uri ImageUrl { get; set; }

            [JsonProperty("last_name")]
            public string LastName { get; set; }

            [JsonProperty("social_connections")]
            public SocialConnections SocialConnections { get; set; }

            [JsonProperty("thumb_url")]
            public Uri ThumbUrl { get; set; }

            [JsonProperty("url")]
            public Uri Url { get; set; }

            [JsonProperty("vanity")]
            public string Vanity { get; set; }
        }

        public partial class SocialConnections
        {
            [JsonProperty("deviantart")]
            public SocialMediaConnection Deviantart { get; set; }

            [JsonProperty("discord")]
            public SocialMediaConnection Discord { get; set; }

            [JsonProperty("facebook")]
            public SocialMediaConnection Facebook { get; set; }

            [JsonProperty("google")]
            public SocialMediaConnection Google { get; set; }

            [JsonProperty("instagram")]
            public SocialMediaConnection Instagram { get; set; }

            [JsonProperty("reddit")]
            public SocialMediaConnection Reddit { get; set; }

            [JsonProperty("spotify")]
            public SocialMediaConnection Spotify { get; set; }

            [JsonProperty("twitch")]
            public SocialMediaConnection Twitch { get; set; }

            [JsonProperty("twitter")]
            public SocialMediaConnection Twitter { get; set; }

            [JsonProperty("vimeo")]
            public SocialMediaConnection Vimeo { get; set; }

            [JsonProperty("youtube")]
            public SocialMediaConnection Youtube { get; set; }
        }

        public partial class SocialMediaConnection
        {
            [JsonProperty("scopes")]
            public string[] Scopes { get; set; }

            [JsonProperty("url")]
            public Uri Url { get; set; }

            [JsonProperty("user_id")]
            public string UserId { get; set; }
        }

        public partial class DataRelationships
        {
            [JsonProperty("memberships")]
            public Memberships Memberships { get; set; }
        }

        public partial class Memberships
        {
            [JsonProperty("data")]
            public Datum[] Data { get; set; }
        }

        public partial class Datum
        {
            [JsonProperty("id")]
            public string Id { get; set; }

            [JsonProperty("type")]
            public string Type { get; set; }
        }

        public partial class Included
        {
            [JsonProperty("attributes")]
            public IncludedAttributes Attributes { get; set; }

            [JsonProperty("id")]
            public string Id { get; set; }

            [JsonProperty("relationships", NullValueHandling = NullValueHandling.Ignore)]
            public IncludedRelationships Relationships { get; set; }

            [JsonProperty("type")]
            public string Type { get; set; }
        }

        public partial class IncludedAttributes
        {
            [JsonProperty("currently_entitled_amount_cents", NullValueHandling = NullValueHandling.Ignore)]
            public long? CurrentlyEntitledAmountCents { get; set; }

            [JsonProperty("patron_status", NullValueHandling = NullValueHandling.Ignore)]
            public string PatronStatus { get; set; }
        }

        public partial class IncludedRelationships
        {
            [JsonProperty("currently_entitled_tiers")]
            public Memberships CurrentlyEntitledTiers { get; set; }
        }

        public partial class Links
        {
            [JsonProperty("self")]
            public Uri Self { get; set; }
        }
    }
    public class AccessTokenRefresh
    {
        [JsonProperty("access_token")]
        public string AccessToken { get; set; }
        [JsonProperty("refresh_token")]
        public string RefreshToken { get; set; }
        [JsonProperty("expires_in")]
        public string ExpiresIn { get; set; }
        [JsonProperty("scope")]
        public string Scope { get; set; }
        [JsonProperty("token_Type")]
        public string TokenType { get; set; }
    }
}

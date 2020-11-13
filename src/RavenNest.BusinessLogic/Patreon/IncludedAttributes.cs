using System;
using Newtonsoft.Json;

namespace RavenNest.BusinessLogic.Patreon
{
    public partial class IncludedAttributes
    {
        [JsonProperty("avatar_photo_url", NullValueHandling = NullValueHandling.Ignore)]
        public Uri AvatarPhotoUrl { get; set; }

        [JsonProperty("campaign_pledge_sum", NullValueHandling = NullValueHandling.Ignore)]
        public long? CampaignPledgeSum { get; set; }

        [JsonProperty("cover_photo_url", NullValueHandling = NullValueHandling.Ignore)]
        public Uri CoverPhotoUrl { get; set; }

        [JsonProperty("created_at")]
        public DateTimeOffset? CreatedAt { get; set; }

        [JsonProperty("creation_count", NullValueHandling = NullValueHandling.Ignore)]
        public long? CreationCount { get; set; }

        [JsonProperty("creation_name", NullValueHandling = NullValueHandling.Ignore)]
        public string CreationName { get; set; }

        [JsonProperty("currency", NullValueHandling = NullValueHandling.Ignore)]
        public string Currency { get; set; }

        [JsonProperty("discord_server_id", NullValueHandling = NullValueHandling.Ignore)]
        public string DiscordServerId { get; set; }

        [JsonProperty("display_patron_goals", NullValueHandling = NullValueHandling.Ignore)]
        public bool? DisplayPatronGoals { get; set; }

        [JsonProperty("earnings_visibility", NullValueHandling = NullValueHandling.Ignore)]
        public string EarningsVisibility { get; set; }

        [JsonProperty("image_small_url", NullValueHandling = NullValueHandling.Ignore)]
        public Uri ImageSmallUrl { get; set; }

        [JsonProperty("image_url")]
        public Uri ImageUrl { get; set; }

        [JsonProperty("is_charge_upfront", NullValueHandling = NullValueHandling.Ignore)]
        public bool? IsChargeUpfront { get; set; }

        [JsonProperty("is_charged_immediately", NullValueHandling = NullValueHandling.Ignore)]
        public bool? IsChargedImmediately { get; set; }

        [JsonProperty("is_monthly", NullValueHandling = NullValueHandling.Ignore)]
        public bool? IsMonthly { get; set; }

        [JsonProperty("is_nsfw", NullValueHandling = NullValueHandling.Ignore)]
        public bool? IsNsfw { get; set; }

        [JsonProperty("is_plural", NullValueHandling = NullValueHandling.Ignore)]
        public bool? IsPlural { get; set; }

        [JsonProperty("main_video_embed", NullValueHandling = NullValueHandling.Ignore)]
        public string MainVideoEmbed { get; set; }

        [JsonProperty("main_video_url", NullValueHandling = NullValueHandling.Ignore)]
        public Uri MainVideoUrl { get; set; }

        [JsonProperty("name", NullValueHandling = NullValueHandling.Ignore)]
        public string Name { get; set; }

        [JsonProperty("one_liner")]
        public object OneLiner { get; set; }

        [JsonProperty("outstanding_payment_amount_cents", NullValueHandling = NullValueHandling.Ignore)]
        public long? OutstandingPaymentAmountCents { get; set; }

        [JsonProperty("patron_count", NullValueHandling = NullValueHandling.Ignore)]
        public long? PatronCount { get; set; }

        [JsonProperty("pay_per_name", NullValueHandling = NullValueHandling.Ignore)]
        public string PayPerName { get; set; }

        [JsonProperty("pledge_sum", NullValueHandling = NullValueHandling.Ignore)]
        public long? PledgeSum { get; set; }

        [JsonProperty("pledge_sum_currency", NullValueHandling = NullValueHandling.Ignore)]
        public string PledgeSumCurrency { get; set; }

        [JsonProperty("pledge_url", NullValueHandling = NullValueHandling.Ignore)]
        public string PledgeUrl { get; set; }

        [JsonProperty("published_at")]
        public DateTimeOffset? PublishedAt { get; set; }

        [JsonProperty("summary", NullValueHandling = NullValueHandling.Ignore)]
        public string Summary { get; set; }

        [JsonProperty("thanks_embed")]
        public object ThanksEmbed { get; set; }

        [JsonProperty("thanks_msg", NullValueHandling = NullValueHandling.Ignore)]
        public string ThanksMsg { get; set; }

        [JsonProperty("thanks_video_url")]
        public object ThanksVideoUrl { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("about")]
        public object About { get; set; }

        [JsonProperty("apple_id")]
        public object AppleId { get; set; }

        [JsonProperty("can_see_nsfw")]
        public object CanSeeNsfw { get; set; }

        [JsonProperty("created", NullValueHandling = NullValueHandling.Ignore)]
        public DateTimeOffset? Created { get; set; }

        [JsonProperty("default_country_code")]
        public object DefaultCountryCode { get; set; }

        [JsonProperty("discord_id")]
        public object DiscordId { get; set; }

        [JsonProperty("email", NullValueHandling = NullValueHandling.Ignore)]
        public string Email { get; set; }

        [JsonProperty("facebook")]
        public object Facebook { get; set; }

        [JsonProperty("facebook_id")]
        public object FacebookId { get; set; }

        [JsonProperty("first_name", NullValueHandling = NullValueHandling.Ignore)]
        public string FirstName { get; set; }

        [JsonProperty("full_name", NullValueHandling = NullValueHandling.Ignore)]
        public string FullName { get; set; }

        [JsonProperty("gender", NullValueHandling = NullValueHandling.Ignore)]
        public long? Gender { get; set; }

        [JsonProperty("google_id", NullValueHandling = NullValueHandling.Ignore)]
        public string GoogleId { get; set; }

        [JsonProperty("has_password", NullValueHandling = NullValueHandling.Ignore)]
        public bool? HasPassword { get; set; }

        [JsonProperty("is_deleted", NullValueHandling = NullValueHandling.Ignore)]
        public bool? IsDeleted { get; set; }

        [JsonProperty("is_email_verified", NullValueHandling = NullValueHandling.Ignore)]
        public bool? IsEmailVerified { get; set; }

        [JsonProperty("is_nuked", NullValueHandling = NullValueHandling.Ignore)]
        public bool? IsNuked { get; set; }

        [JsonProperty("is_suspended", NullValueHandling = NullValueHandling.Ignore)]
        public bool? IsSuspended { get; set; }

        [JsonProperty("last_name", NullValueHandling = NullValueHandling.Ignore)]
        public string LastName { get; set; }

        [JsonProperty("patron_currency")]
        public string PatronCurrency { get; set; }

        [JsonProperty("social_connections", NullValueHandling = NullValueHandling.Ignore)]
        public SocialConnections SocialConnections { get; set; }

        [JsonProperty("thumb_url", NullValueHandling = NullValueHandling.Ignore)]
        public Uri ThumbUrl { get; set; }

        [JsonProperty("twitch")]
        public object Twitch { get; set; }

        [JsonProperty("twitter")]
        public object Twitter { get; set; }

        [JsonProperty("vanity", NullValueHandling = NullValueHandling.Ignore)]
        public string Vanity { get; set; }

        [JsonProperty("youtube")]
        public object Youtube { get; set; }

        [JsonProperty("amount", NullValueHandling = NullValueHandling.Ignore)]
        public long? Amount { get; set; }

        [JsonProperty("amount_cents", NullValueHandling = NullValueHandling.Ignore)]
        public long? AmountCents { get; set; }

        [JsonProperty("description", NullValueHandling = NullValueHandling.Ignore)]
        public string Description { get; set; }

        [JsonProperty("remaining")]
        public long? Remaining { get; set; }

        [JsonProperty("requires_shipping", NullValueHandling = NullValueHandling.Ignore)]
        public bool? RequiresShipping { get; set; }

        [JsonProperty("user_limit")]
        public object UserLimit { get; set; }

        [JsonProperty("discord_role_ids", NullValueHandling = NullValueHandling.Ignore)]
        public string[] DiscordRoleIds { get; set; }

        [JsonProperty("edited_at", NullValueHandling = NullValueHandling.Ignore)]
        public DateTimeOffset? EditedAt { get; set; }

        [JsonProperty("patron_amount_cents", NullValueHandling = NullValueHandling.Ignore)]
        public long? PatronAmountCents { get; set; }

        [JsonProperty("post_count", NullValueHandling = NullValueHandling.Ignore)]
        public long? PostCount { get; set; }

        [JsonProperty("published", NullValueHandling = NullValueHandling.Ignore)]
        public bool? Published { get; set; }

        [JsonProperty("title", NullValueHandling = NullValueHandling.Ignore)]
        public string Title { get; set; }

        [JsonProperty("unpublished_at")]
        public DateTimeOffset? UnpublishedAt { get; set; }

        [JsonProperty("welcome_message")]
        public object WelcomeMessage { get; set; }

        [JsonProperty("welcome_message_unsafe")]
        public object WelcomeMessageUnsafe { get; set; }

        [JsonProperty("welcome_video_embed")]
        public object WelcomeVideoEmbed { get; set; }

        [JsonProperty("welcome_video_url")]
        public object WelcomeVideoUrl { get; set; }

        [JsonProperty("completed_percentage", NullValueHandling = NullValueHandling.Ignore)]
        public long? CompletedPercentage { get; set; }

        [JsonProperty("reached_at")]
        public object ReachedAt { get; set; }
    }
}

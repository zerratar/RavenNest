namespace RavenNest.PatreonAPI
{
    using System;
    using System.Collections.Generic;

    using System.Globalization;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    public partial class PatreonMembersData
    {
        [JsonProperty("data")]
        public List<Datum> Data { get; set; }

        [JsonProperty("included")]
        public List<Included> Included { get; set; }

        [JsonProperty("links")]
        public CursorsClass Links { get; set; }

        [JsonProperty("meta")]
        public Meta Meta { get; set; }
    }

    public partial class Datum
    {
        [JsonProperty("attributes")]
        public DatumAttributes Attributes { get; set; }

        [JsonProperty("id")]
        public Guid Id { get; set; }

        [JsonProperty("relationships")]
        public Relationships Relationships { get; set; }

        [JsonProperty("type")]
        public PurpleType Type { get; set; }
    }

    public partial class DatumAttributes
    {
        [JsonProperty("campaign_lifetime_support_cents")]
        public long CampaignLifetimeSupportCents { get; set; }

        [JsonProperty("currently_entitled_amount_cents")]
        public long CurrentlyEntitledAmountCents { get; set; }

        [JsonProperty("email")]
        public string Email { get; set; }

        [JsonProperty("full_name")]
        public string FullName { get; set; }

        [JsonProperty("is_follower")]
        public bool IsFollower { get; set; }

        [JsonProperty("last_charge_date")]
        public DateTimeOffset? LastChargeDate { get; set; }

        [JsonProperty("last_charge_status")]
        public Status? LastChargeStatus { get; set; }

        [JsonProperty("lifetime_support_cents")]
        public long LifetimeSupportCents { get; set; }

        [JsonProperty("next_charge_date")]
        public DateTimeOffset? NextChargeDate { get; set; }

        [JsonProperty("note")]
        public string Note { get; set; }

        [JsonProperty("patron_status")]
        public PatronStatus? PatronStatus { get; set; }

        [JsonProperty("pledge_cadence")]
        public long? PledgeCadence { get; set; }

        [JsonProperty("pledge_relationship_start")]
        public DateTimeOffset? PledgeRelationshipStart { get; set; }

        [JsonProperty("will_pay_amount_cents")]
        public long WillPayAmountCents { get; set; }
    }

    public partial class Relationships
    {
        [JsonProperty("currently_entitled_tiers")]
        public CurrentlyEntitledTiers CurrentlyEntitledTiers { get; set; }

        [JsonProperty("pledge_history")]
        public CurrentlyEntitledTiers PledgeHistory { get; set; }

        [JsonProperty("user")]
        public User User { get; set; }
    }

    public partial class CurrentlyEntitledTiers
    {
        [JsonProperty("data")]
        public List<Dat> Data { get; set; }
    }

    public partial class Dat
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("type")]
        public DataType Type { get; set; }
    }

    public partial class User
    {
        [JsonProperty("data")]
        public Dat Data { get; set; }

        [JsonProperty("links")]
        public UserLinks Links { get; set; }
    }

    public partial class UserLinks
    {
        [JsonProperty("related")]
        public Uri Related { get; set; }
    }

    public partial class Included
    {
        [JsonProperty("attributes")]
        public IncludedAttributes Attributes { get; set; }

        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("type")]
        public DataType Type { get; set; }
    }

    public partial class IncludedAttributes
    {
        [JsonProperty("about")]
        public string About { get; set; }

        [JsonProperty("created", NullValueHandling = NullValueHandling.Ignore)]
        public DateTimeOffset? Created { get; set; }

        [JsonProperty("first_name", NullValueHandling = NullValueHandling.Ignore)]
        public string FirstName { get; set; }

        [JsonProperty("full_name", NullValueHandling = NullValueHandling.Ignore)]
        public string FullName { get; set; }

        [JsonProperty("hide_pledges", NullValueHandling = NullValueHandling.Ignore)]
        public bool? HidePledges { get; set; }

        [JsonProperty("image_url", NullValueHandling = NullValueHandling.Ignore)]
        public Uri ImageUrl { get; set; }

        [JsonProperty("last_name", NullValueHandling = NullValueHandling.Ignore)]
        public string LastName { get; set; }

        [JsonProperty("like_count", NullValueHandling = NullValueHandling.Ignore)]
        public long? LikeCount { get; set; }

        [JsonProperty("thumb_url", NullValueHandling = NullValueHandling.Ignore)]
        public Uri ThumbUrl { get; set; }

        [JsonProperty("url", NullValueHandling = NullValueHandling.Ignore)]
        public Uri Url { get; set; }

        [JsonProperty("vanity")]
        public string Vanity { get; set; }

        [JsonProperty("amount_cents", NullValueHandling = NullValueHandling.Ignore)]
        public long? AmountCents { get; set; }

        [JsonProperty("currency_code", NullValueHandling = NullValueHandling.Ignore)]
        public CurrencyCode? CurrencyCode { get; set; }

        [JsonProperty("date", NullValueHandling = NullValueHandling.Ignore)]
        public DateTimeOffset? Date { get; set; }

        [JsonProperty("payment_status")]
        public Status? PaymentStatus { get; set; }

        [JsonProperty("pledge_payment_status")]
        public PledgePaymentStatus? PledgePaymentStatus { get; set; }

        [JsonProperty("tier_id", NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(ParseStringConverter))]
        public long? TierId { get; set; }

        [JsonProperty("tier_title", NullValueHandling = NullValueHandling.Ignore)]
        public TierTitle? TierTitle { get; set; }

        [JsonProperty("type", NullValueHandling = NullValueHandling.Ignore)]
        public AttributesType? Type { get; set; }
    }

    public partial class CursorsClass
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
        public CursorsClass Cursors { get; set; }

        [JsonProperty("total")]
        public long Total { get; set; }
    }

    public enum Status { Declined, Paid };

    public enum PatronStatus { ActivePatron, DeclinedPatron, FormerPatron };

    public enum DataType { PledgeEvent, Tier, User, Address };

    public enum PurpleType { Member };

    public enum CurrencyCode { Aud, Eur, Gbp, Usd, Cad, Unknown };

    public enum PledgePaymentStatus { Declined, Valid };

    public enum TierTitle { Steel, Mithril, Adamantite, Rune, Dragon, Abraxas, Phantom };

    public enum AttributesType { PledgeDelete, PledgeStart, PedgeDowngrade, PledgeUpgrade, Subscription };

    internal static class PatreonMembersConverter
    {
        public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
            DateParseHandling = DateParseHandling.None,
            Converters =
            {
                StatusConverter.Singleton,
                PatronStatusConverter.Singleton,
                DataTypeConverter.Singleton,
                PurpleTypeConverter.Singleton,
                CurrencyCodeConverter.Singleton,
                PledgePaymentStatusConverter.Singleton,
                TierTitleConverter.Singleton,
                AttributesTypeConverter.Singleton,
                new IsoDateTimeConverter { DateTimeStyles = DateTimeStyles.AssumeUniversal }
            },
        };
    }

    internal class StatusConverter : JsonConverter
    {
        public override bool CanConvert(Type t) => t == typeof(Status) || t == typeof(Status?);

        public override object ReadJson(JsonReader reader, Type t, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null) return null;
            var value = serializer.Deserialize<string>(reader);
            switch (value)
            {
                case "Declined":
                    return Status.Declined;
                case "Paid":
                    return Status.Paid;
            }
            throw new Exception("Cannot unmarshal type Status");
        }

        public override void WriteJson(JsonWriter writer, object untypedValue, JsonSerializer serializer)
        {
            if (untypedValue == null)
            {
                serializer.Serialize(writer, null);
                return;
            }
            var value = (Status)untypedValue;
            switch (value)
            {
                case Status.Declined:
                    serializer.Serialize(writer, "Declined");
                    return;
                case Status.Paid:
                    serializer.Serialize(writer, "Paid");
                    return;
            }
            throw new Exception("Cannot marshal type Status");
        }

        public static readonly StatusConverter Singleton = new StatusConverter();
    }

    internal class PatronStatusConverter : JsonConverter
    {
        public override bool CanConvert(Type t) => t == typeof(PatronStatus) || t == typeof(PatronStatus?);

        public override object ReadJson(JsonReader reader, Type t, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null) return null;
            var value = serializer.Deserialize<string>(reader);
            switch (value)
            {
                case "active_patron":
                    return PatronStatus.ActivePatron;
                case "declined_patron":
                    return PatronStatus.DeclinedPatron;
                case "former_patron":
                    return PatronStatus.FormerPatron;
            }
            throw new Exception("Cannot unmarshal type PatronStatus");
        }

        public override void WriteJson(JsonWriter writer, object untypedValue, JsonSerializer serializer)
        {
            if (untypedValue == null)
            {
                serializer.Serialize(writer, null);
                return;
            }
            var value = (PatronStatus)untypedValue;
            switch (value)
            {
                case PatronStatus.ActivePatron:
                    serializer.Serialize(writer, "active_patron");
                    return;
                case PatronStatus.DeclinedPatron:
                    serializer.Serialize(writer, "declined_patron");
                    return;
                case PatronStatus.FormerPatron:
                    serializer.Serialize(writer, "former_patron");
                    return;
            }
            throw new Exception("Cannot marshal type PatronStatus");
        }

        public static readonly PatronStatusConverter Singleton = new PatronStatusConverter();
    }

    internal class DataTypeConverter : JsonConverter
    {
        public override bool CanConvert(Type t) => t == typeof(DataType) || t == typeof(DataType?);

        public override object ReadJson(JsonReader reader, Type t, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null) return null;
            var value = serializer.Deserialize<string>(reader);
            switch (value)
            {
                case "pledge-event":
                    return DataType.PledgeEvent;
                case "tier":
                    return DataType.Tier;
                case "user":
                    return DataType.User;
                case "address":
                    return DataType.Address;
            }
            throw new Exception("Cannot unmarshal type DataType");
        }

        public override void WriteJson(JsonWriter writer, object untypedValue, JsonSerializer serializer)
        {
            if (untypedValue == null)
            {
                serializer.Serialize(writer, null);
                return;
            }
            var value = (DataType)untypedValue;
            switch (value)
            {
                case DataType.PledgeEvent:
                    serializer.Serialize(writer, "pledge-event");
                    return;
                case DataType.Tier:
                    serializer.Serialize(writer, "tier");
                    return;
                case DataType.User:
                    serializer.Serialize(writer, "user");
                    return;
            }
            throw new Exception("Cannot marshal type DataType");
        }

        public static readonly DataTypeConverter Singleton = new DataTypeConverter();
    }

    internal class PurpleTypeConverter : JsonConverter
    {
        public override bool CanConvert(Type t) => t == typeof(PurpleType) || t == typeof(PurpleType?);

        public override object ReadJson(JsonReader reader, Type t, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null) return null;
            var value = serializer.Deserialize<string>(reader);
            if (value == "member")
            {
                return PurpleType.Member;
            }
            throw new Exception("Cannot unmarshal type PurpleType");
        }

        public override void WriteJson(JsonWriter writer, object untypedValue, JsonSerializer serializer)
        {
            if (untypedValue == null)
            {
                serializer.Serialize(writer, null);
                return;
            }
            var value = (PurpleType)untypedValue;
            if (value == PurpleType.Member)
            {
                serializer.Serialize(writer, "member");
                return;
            }
            throw new Exception("Cannot marshal type PurpleType");
        }

        public static readonly PurpleTypeConverter Singleton = new PurpleTypeConverter();
    }

    internal class CurrencyCodeConverter : JsonConverter
    {
        public override bool CanConvert(Type t) => t == typeof(CurrencyCode) || t == typeof(CurrencyCode?);

        public override object ReadJson(JsonReader reader, Type t, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null) return null;
            var value = serializer.Deserialize<string>(reader);
            switch (value)
            {
                case "AUD":
                    return CurrencyCode.Aud;
                case "EUR":
                    return CurrencyCode.Eur;
                case "GBP":
                    return CurrencyCode.Gbp;
                case "USD":
                    return CurrencyCode.Usd;
                case "CAD":
                    return CurrencyCode.Cad;
                default:
                    return CurrencyCode.Unknown;
            }
            throw new Exception("Cannot unmarshal type CurrencyCode");
        }

        public override void WriteJson(JsonWriter writer, object untypedValue, JsonSerializer serializer)
        {
            if (untypedValue == null)
            {
                serializer.Serialize(writer, null);
                return;
            }
            var value = (CurrencyCode)untypedValue;
            switch (value)
            {
                case CurrencyCode.Aud:
                    serializer.Serialize(writer, "AUD");
                    return;
                case CurrencyCode.Eur:
                    serializer.Serialize(writer, "EUR");
                    return;
                case CurrencyCode.Gbp:
                    serializer.Serialize(writer, "GBP");
                    return;
                case CurrencyCode.Usd:
                    serializer.Serialize(writer, "USD");
                    return;
                case CurrencyCode.Cad:
                    serializer.Serialize(writer, "CAD");
                    return;
            }
            throw new Exception("Cannot marshal type CurrencyCode");
        }

        public static readonly CurrencyCodeConverter Singleton = new CurrencyCodeConverter();
    }

    internal class PledgePaymentStatusConverter : JsonConverter
    {
        public override bool CanConvert(Type t) => t == typeof(PledgePaymentStatus) || t == typeof(PledgePaymentStatus?);

        public override object ReadJson(JsonReader reader, Type t, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null) return null;
            var value = serializer.Deserialize<string>(reader);
            switch (value)
            {
                case "declined":
                    return PledgePaymentStatus.Declined;
                case "valid":
                    return PledgePaymentStatus.Valid;
            }
            throw new Exception("Cannot unmarshal type PledgePaymentStatus");
        }

        public override void WriteJson(JsonWriter writer, object untypedValue, JsonSerializer serializer)
        {
            if (untypedValue == null)
            {
                serializer.Serialize(writer, null);
                return;
            }
            var value = (PledgePaymentStatus)untypedValue;
            switch (value)
            {
                case PledgePaymentStatus.Declined:
                    serializer.Serialize(writer, "declined");
                    return;
                case PledgePaymentStatus.Valid:
                    serializer.Serialize(writer, "valid");
                    return;
            }
            throw new Exception("Cannot marshal type PledgePaymentStatus");
        }

        public static readonly PledgePaymentStatusConverter Singleton = new PledgePaymentStatusConverter();
    }

    internal class ParseStringConverter : JsonConverter
    {
        public override bool CanConvert(Type t) => t == typeof(long) || t == typeof(long?);

        public override object ReadJson(JsonReader reader, Type t, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null) return null;
            var value = serializer.Deserialize<string>(reader);
            long l;
            if (Int64.TryParse(value, out l))
            {
                return l;
            }
            throw new Exception("Cannot unmarshal type long");
        }

        public override void WriteJson(JsonWriter writer, object untypedValue, JsonSerializer serializer)
        {
            if (untypedValue == null)
            {
                serializer.Serialize(writer, null);
                return;
            }
            var value = (long)untypedValue;
            serializer.Serialize(writer, value.ToString());
            return;
        }

        public static readonly ParseStringConverter Singleton = new ParseStringConverter();
    }

    internal class TierTitleConverter : JsonConverter
    {
        public override bool CanConvert(Type t) => t == typeof(TierTitle) || t == typeof(TierTitle?);

        public override object ReadJson(JsonReader reader, Type t, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null) return null;
            var value = serializer.Deserialize<string>(reader);
            switch (value)
            {
                case "Phantom":
                    return TierTitle.Phantom;
                case "Abraxas":
                    return TierTitle.Abraxas;
                case "Dragon":
                    return TierTitle.Dragon;
                case "Rune":
                    return TierTitle.Rune;
                case "Adamantite":
                    return TierTitle.Adamantite;
                case "Mithril":
                    return TierTitle.Mithril;
                case "Steel":
                    return TierTitle.Steel;
            }
            throw new Exception("Cannot unmarshal type TierTitle");
        }

        public override void WriteJson(JsonWriter writer, object untypedValue, JsonSerializer serializer)
        {
            if (untypedValue == null)
            {
                serializer.Serialize(writer, null);
                return;
            }
            var value = (TierTitle)untypedValue;
            switch (value)
            {
                case TierTitle.Phantom:
                    serializer.Serialize(writer, "Phantom");
                    return;
                case TierTitle.Abraxas:
                    serializer.Serialize(writer, "Abraxas");
                    return;
                case TierTitle.Dragon:
                    serializer.Serialize(writer, "Dragon");
                    return;
                case TierTitle.Rune:
                    serializer.Serialize(writer, "Rune");
                    return;
                case TierTitle.Adamantite:
                    serializer.Serialize(writer, "Adamantite");
                    return;
                case TierTitle.Mithril:
                    serializer.Serialize(writer, "Mithril");
                    return;
                case TierTitle.Steel:
                    serializer.Serialize(writer, "Steel");
                    return;
            }
            throw new Exception("Cannot marshal type TierTitle");
        }

        public static readonly TierTitleConverter Singleton = new TierTitleConverter();
    }

    internal class AttributesTypeConverter : JsonConverter
    {
        public override bool CanConvert(Type t) => t == typeof(AttributesType) || t == typeof(AttributesType?);

        public override object ReadJson(JsonReader reader, Type t, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null) return null;
            var value = serializer.Deserialize<string>(reader);
            switch (value)
            {
                case "pledge_delete":
                    return AttributesType.PledgeDelete;
                case "pledge_start":
                    return AttributesType.PledgeStart;
                case "pledge_downgrade":
                    return AttributesType.PedgeDowngrade;
                case "pledge_upgrade":
                    return AttributesType.PledgeUpgrade;
                case "subscription":
                    return AttributesType.Subscription;
            }
            throw new Exception("Cannot unmarshal type AttributesType");
        }

        public override void WriteJson(JsonWriter writer, object untypedValue, JsonSerializer serializer)
        {
            if (untypedValue == null)
            {
                serializer.Serialize(writer, null);
                return;
            }
            var value = (AttributesType)untypedValue;
            switch (value)
            {
                case AttributesType.PledgeDelete:
                    serializer.Serialize(writer, "pledge_delete");
                    return;
                case AttributesType.PledgeStart:
                    serializer.Serialize(writer, "pledge_start");
                    return;
                case AttributesType.Subscription:
                    serializer.Serialize(writer, "subscription");
                    return;
            }
            throw new Exception("Cannot marshal type AttributesType");
        }

        public static readonly AttributesTypeConverter Singleton = new AttributesTypeConverter();
    }
}

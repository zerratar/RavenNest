using System;
using System.Collections.Generic;

namespace RavenNest.Models
{
    public class SessionToken
    {
        public Guid SessionId { get; set; }
        public DateTime StartedUtc { get; set; }
        public DateTime ExpiresUtc { get; set; }
        public string AuthToken { get; set; }
        public bool Expired => DateTime.UtcNow >= ExpiresUtc;
        public string TwitchUserId { get; set; }
        public string TwitchUserName { get; set; }
        public string TwitchDisplayName { get; set; }
        public string ClientVersion { get; set; }
    }

    public class BeginSessionResult
    {
        public SessionToken SessionToken { get; set; }
        public ExpMultiplier ExpMultiplier { get; set; }
        public VillageInfo Village { get; set; }
        public Permissions Permissions { get; set; }
        public string ExpectedClientVersion { get; set; }
        public BeginSessionResultState State { get; set; }
        public static BeginSessionResult InvalidVersion { get; set; } = new BeginSessionResult
        {
            State = BeginSessionResultState.UpdateRequired
        };
    }

    public class VillageInfo
    {
        public int Level { get; set; }
        public long Experience { get; set; }
        public string Name { get; set; }
        public IReadOnlyList<VillageHouseInfo> Houses { get; set; }
    }

    public class VillageHouseInfo
    {
        public string Owner { get; set; }
        public int Type { get; set; }
        public int Slot { get; set; }
    }
    public class Permissions
    {
        public bool IsAdministrator { get; set; }
        public bool IsModerator { get; set; }
        public int SubscriberTier { get; set; }
        public int ExpMultiplierLimit { get; set; }
        public bool StrictLevelRequirements { get; set; }
    }
    public class ExpMultiplier
    {
        public string EventName { get; set; }
        public int Multiplier { get; set; }
        public bool StartedByPlayer { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
    }

    public enum BeginSessionResultState
    {
        Success,
        UpdateRequired,
        AccountDisabled,
        UnknownError
    }

    public class RedeemableItem
    {
        public Guid Id { get; set; }
        public Guid ItemId { get; set; }
        public Guid CurrencyItemId { get; set; }
        public int Cost { get; set; }
        public int Amount { get; set; }
        public string AvailableDateRange { get; set; }
    }

    public class RedeemItemResult
    {
        public RedeemItemResultCode Code { get; set; }
        public Guid RedeemedItemId { get; set; }
        public int RedeemedItemAmount { get; set; }
        public Guid CurrencyItemId { get; set; }
        public long CurrencyLeft { get; set; }
        public int CurrencyCost { get; set; }
        public string ErrorMessage { get; set; }
        public static RedeemItemResult Ok()
        {
            return new RedeemItemResult
            {
                Code = RedeemItemResultCode.Success
            };
        }
        public static RedeemItemResult NoSuchItem(string itemName = null)
        {
            return new RedeemItemResult
            {
                Code = RedeemItemResultCode.NoSuchItem,
                //ErrorMessage = itemName != null ? itemName + " is not a redeemable item."
            };
        }
        public static RedeemItemResult Error(string message)
        {
            return new RedeemItemResult
            {
                Code = RedeemItemResultCode.InsufficientCurrency,
                ErrorMessage = message
            };
        }
        public static RedeemItemResult InsufficientCurrency(long actualAmount, int expectedAmount, string currency)
        {
            return new RedeemItemResult
            {
                Code = RedeemItemResultCode.InsufficientCurrency,
                ErrorMessage = "You don't have enough " + currency + " to redeem this item; You need " + (expectedAmount - actualAmount) + " more."
            };
        }
    }

    public enum RedeemItemResultCode
    {
        Success,
        InsufficientCurrency,
        NoSuchItem,
        Error
    }
}

using System;
using System.Collections.Generic;

namespace RavenNest.Models
{
    public class SessionToken
    {
        public Guid UserId { get; set; }
        public Guid SessionId { get; set; }
        public DateTime StartedUtc { get; set; }
        public DateTime ExpiresUtc { get; set; }
        public string AuthToken { get; set; }
        public bool Expired => DateTime.UtcNow >= ExpiresUtc;
        public string UserName { get; set; }
        public string DisplayName { get; set; }

        [Obsolete("Use UserId instead.")]
        public string TwitchUserId { get; set; }

        [Obsolete("Use UserName instead.")]
        public string TwitchUserName { get; set; }
        [Obsolete("Use DisplayName instead.")]
        public string TwitchDisplayName { get; set; }
        public string ClientVersion { get; set; }
    }

    public class BeginSessionResult
    {
        public SessionToken SessionToken { get; set; }
        public ExpMultiplier ExpMultiplier { get; set; }
        public VillageInfo Village { get; set; }
        public SessionSettings Permissions { get; set; }
        public string ExpectedClientVersion { get; set; }
        public BeginSessionResultState State { get; set; }
        public Dictionary<string, object> UserSettings { get; set; }
        public static BeginSessionResult InvalidVersion { get; set; } = new BeginSessionResult
        {
            State = BeginSessionResultState.UpdateRequired
        };
    }

    public class GiftItemResult
    {
        public static GiftItemResult Error { get; } = new GiftItemResult { Status = GiftItemStatus.ErrUnknown };
        public static GiftItemResult InventoryError { get; } = new GiftItemResult { Status = GiftItemStatus.ErrInventoryLock };
        public static GiftItemResult SoulboundItem { get; } = new GiftItemResult { Status = GiftItemStatus.ErrSoulboundItem };
        public static GiftItemResult NoItem { get; } = new GiftItemResult { Status = GiftItemStatus.ErrNoItem };
        public InventoryItem StackToIncrement { get; set; }
        public InventoryItem StackToDecrement { get; set; }
        public long Amount { get; set; }
        public GiftItemStatus Status { get; set; }

        public static GiftItemResult OK(long amount, InventoryItem stackToIncrement, InventoryItem stackToDecrement)
        {
            return new GiftItemResult
            {
                Status = GiftItemStatus.OK,
                Amount = amount,
                StackToIncrement = stackToIncrement,
                StackToDecrement = stackToDecrement
            };
        }
    }

    public enum GiftItemStatus
    {
        ErrServerUnavailable,
        ErrUnknown,
        ErrInventoryLock,
        ErrSoulboundItem,
        ErrNoItem,
        OK
    }

    public class VillageInfo
    {
        public int Level { get; set; }
        public long Experience { get; set; }
        public string Name { get; set; }
        public long Coins { get; set; }
        public long Wood { get; set; }
        public long Ore { get; set; }
        public long Wheat { get; set; }
        public long Fish { get; set; }
        public IReadOnlyList<VillageHouseInfo> Houses { get; set; }
    }

    public class VillageHouseInfo
    {
        public string Owner { get; set; }
        public Guid? OwnerCharacterId { get; set; }
        public Guid? OwnerUserId { get; set; }
        public int Type { get; set; }
        public int Slot { get; set; }
    }
    public class SessionSettings
    {
        public bool IsAdministrator { get; set; }
        public bool IsModerator { get; set; }
        public int SubscriberTier { get; set; }
        public int ExpMultiplierLimit { get; set; }
        public int PlayerExpMultiplierLimit { get; set; }
        public bool StrictLevelRequirements { get; set; }
        public double DungeonExpFactor { get; set; }
        public double RaidExpFactor { get; set; }

        public int AutoRestCost { get; set; }
        public int AutoJoinDungeonCost { get; set; }
        public int AutoJoinRaidCost { get; set; }

        public double XP_EasyLevel { get; set; }
        public double XP_IncrementMins { get; set; }
        public double XP_EasyLevelIncrementDivider { get; set; }
        public double XP_GlobalMultiplierFactor { get; set; }
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
        public Guid InventoryItemId { get; set; }
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

using System;
using System.Collections.Generic;

namespace RavenNest.Models
{
    /// <summary>Where a listed item came from, and where it goes back to.</summary>
    public enum StreamerShopSourceKind
    {
        Stash = 0,
        Character = 1
    }

    /// <summary>Who may buy from a listing.</summary>
    public enum StreamerShopAccess
    {
        /// <summary>Anyone playing in the stream.</summary>
        Everyone = 0,
        /// <summary>Members of the clan the streamer owns.</summary>
        Clan = 1,
        /// <summary>Members of the streamer's clan with one of the chosen roles.</summary>
        ClanRoles = 2
    }

    /// <summary>The tavern shelves of one stream, as the game shows them.</summary>
    public class StreamerShopInfo
    {
        /// <summary>How many shelves the village has, from its town hall tier.</summary>
        public int Shelves { get; set; }
        public long VillageCoins { get; set; }
        public long PriceCap { get; set; }
        public int MinHours { get; set; }
        public int MaxHours { get; set; }
        public List<StreamerShopListingInfo> Listings { get; set; } = new List<StreamerShopListingInfo>();
    }

    public class StreamerShopListingInfo
    {
        public Guid Id { get; set; }
        public int Shelf { get; set; }
        public Guid ItemId { get; set; }
        public long Amount { get; set; }
        public long PricePerItem { get; set; }
        public string Name { get; set; }
        public string Enchantment { get; set; }
        public string Tag { get; set; }
        public Guid? TransmogrificationId { get; set; }
        public int Flags { get; set; }
        public StreamerShopSourceKind SourceKind { get; set; }
        public Guid? SourceCharacterId { get; set; }
        public StreamerShopAccess Access { get; set; }
        public List<Guid> AllowedClanRoleIds { get; set; } = new List<Guid>();
        public int MaxPerViewer { get; set; }
        public long Sold { get; set; }
        public long CoinsEarned { get; set; }
        public DateTime Created { get; set; }
        public DateTime Expires { get; set; }
    }

    /// <summary>Putting an item on a shelf, or changing a listing (only the price, time and access change).</summary>
    public class StreamerShopListingRequest
    {
        public int Shelf { get; set; }
        public StreamerShopSourceKind SourceKind { get; set; }
        public Guid? SourceCharacterId { get; set; }

        // the stack to list, matched the way stacking is matched everywhere
        public Guid ItemId { get; set; }
        public string Tag { get; set; }
        public string Enchantment { get; set; }
        public Guid? TransmogrificationId { get; set; }
        public string Name { get; set; }
        public int Flags { get; set; }

        public long Amount { get; set; }
        public long PricePerItem { get; set; }
        public int DurationHours { get; set; }
        public StreamerShopAccess Access { get; set; }
        public List<Guid> AllowedClanRoleIds { get; set; } = new List<Guid>();
        public int MaxPerViewer { get; set; }
    }

    public class StreamerShopResult
    {
        public bool Ok { get; set; }
        /// <summary>Why not, written for the streamer.</summary>
        public string Error { get; set; }
        public StreamerShopListingInfo Listing { get; set; }
        public StreamerShopInfo Shop { get; set; }
    }

    /// <summary>What the streamer can put on a shelf: the stash, and each of their characters in the stream.</summary>
    public class StreamerShopSources
    {
        public List<StreamerShopSource> Sources { get; set; } = new List<StreamerShopSource>();
        /// <summary>The roles of the clan the streamer owns, for limiting a listing to them. Empty without a clan.</summary>
        public List<StreamerShopClanRole> ClanRoles { get; set; } = new List<StreamerShopClanRole>();
        public bool HasClan { get; set; }
    }

    public class StreamerShopSource
    {
        public StreamerShopSourceKind Kind { get; set; }
        public Guid? CharacterId { get; set; }
        public string Name { get; set; }
        public List<StreamerShopSellableItem> Items { get; set; } = new List<StreamerShopSellableItem>();
    }

    public class StreamerShopSellableItem
    {
        public Guid ItemId { get; set; }
        public string ItemName { get; set; }
        public long Amount { get; set; }
        public string Tag { get; set; }
        public string Enchantment { get; set; }
        public Guid? TransmogrificationId { get; set; }
        public string Name { get; set; }
        public int Flags { get; set; }
        /// <summary>What a vendor pays for one, as a hint for the price.</summary>
        public long VendorPrice { get; set; }
        public bool Sellable { get; set; }
        /// <summary>Why not, when not sellable.</summary>
        public string Reason { get; set; }
    }

    public class StreamerShopClanRole
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public int Level { get; set; }
    }

    public enum StreamerShopBuyResultCode
    {
        Success = 0,
        NotFound = 1,
        SoldOut = 2,
        NotAllowed = 3,
        LimitReached = 4,
        InsufficientCoins = 5,
        OwnListing = 6,
        NotInSession = 7,
        Error = 8
    }

    public class StreamerShopBuyResult
    {
        public StreamerShopBuyResultCode Code { get; set; }
        public string Error { get; set; }
        public Guid ListingId { get; set; }
        public int Shelf { get; set; }
        public Guid ItemId { get; set; }
        public string ItemName { get; set; }
        public Guid InventoryItemId { get; set; }
        public long Price { get; set; }
        public long CoinsLeft { get; set; }
        public long LoyaltyPoints { get; set; }
        /// <summary>How many are left on the shelf after this purchase.</summary>
        public long AmountLeft { get; set; }
    }

    /// <summary>The sales report tab: every sale, newest first, with totals.</summary>
    public class StreamerShopReport
    {
        public long TotalCoins { get; set; }
        public long TotalItems { get; set; }
        public long CoinsLast24Hours { get; set; }
        public List<StreamerShopSale> Sales { get; set; } = new List<StreamerShopSale>();
    }

    public class StreamerShopSale
    {
        public DateTime Time { get; set; }
        public string BuyerName { get; set; }
        public Guid ItemId { get; set; }
        public string ItemName { get; set; }
        public long Amount { get; set; }
        public long Price { get; set; }
        public long LoyaltyPoints { get; set; }
    }
}

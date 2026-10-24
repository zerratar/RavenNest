using RavenNest.DataAnnotations;
using System;

namespace RavenNest.DataModels
{
    /// <summary>
    ///     An item a streamer has put on a tavern shelf for viewers to buy with coins.
    /// </summary>
    /// <remarks>
    ///     The row is the escrow, as <see cref="MarketItem"/> is for the marketplace: when the listing
    ///     is made the items leave the streamer's stash or character and live here, every instance
    ///     field included, until they are bought, taken down or run out of time. What a viewer pays
    ///     goes to the streamer's village, never to the streamer.
    /// </remarks>
    public partial class StreamerShopListing : Entity<StreamerShopListing>
    {
        /// <summary>The streamer whose tavern this shelf is in, the owner of the session.</summary>
        [PersistentData] private Guid streamerUserId;

        /// <summary>Which shelf, from 0.</summary>
        [PersistentData] private int shelf;

        [PersistentData] private Guid itemId;

        /// <summary>How many are left on the shelf.</summary>
        [PersistentData] private long amount;

        [PersistentData] private long pricePerItem;
        [PersistentData] private string name;
        [PersistentData] private string enchantment;
        [PersistentData] private string tag;
        [PersistentData] private Guid? transmogrificationId;
        [PersistentData] private int flags;

        /// <summary>0 the account stash, 1 a character (<see cref="sourceCharacterId"/>).</summary>
        [PersistentData] private int sourceKind;
        [PersistentData] private Guid? sourceCharacterId;

        /// <summary>0 everyone in the stream, 1 the streamer's clan, 2 chosen roles of that clan.</summary>
        [PersistentData] private int access;

        /// <summary>The clan role ids allowed when access is 2, separated by commas.</summary>
        [PersistentData] private string allowedClanRoleIds;

        /// <summary>How many one viewer may buy, 0 for no limit.</summary>
        [PersistentData] private int maxPerViewer;

        [PersistentData] private long sold;

        /// <summary>The coins the listing has brought the village so far.</summary>
        [PersistentData] private long coinsEarned;

        [PersistentData] private DateTime created;
        [PersistentData] private DateTime expires;
    }
}

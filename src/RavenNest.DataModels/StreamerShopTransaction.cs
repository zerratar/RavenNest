using RavenNest.DataAnnotations;
using System;

namespace RavenNest.DataModels
{
    /// <summary>
    ///     One purchase from a tavern shelf: who bought what, for how much, and the loyalty points it
    ///     earned them.
    /// </summary>
    /// <remarks>
    ///     The audit trail, and what the streamer's sales report reads. It outlives its listing: a
    ///     listing that sells out is removed, its sales are not.
    /// </remarks>
    public partial class StreamerShopTransaction : Entity<StreamerShopTransaction>
    {
        [PersistentData] private Guid listingId;
        [PersistentData] private Guid streamerUserId;
        [PersistentData] private Guid buyerUserId;
        [PersistentData] private Guid buyerCharacterId;
        [PersistentData] private Guid itemId;
        [PersistentData] private string itemName;
        [PersistentData] private long amount;

        /// <summary>The coins paid, all of which went to the village.</summary>
        [PersistentData] private long price;

        [PersistentData] private long loyaltyPoints;
        [PersistentData] private DateTime created;
    }
}

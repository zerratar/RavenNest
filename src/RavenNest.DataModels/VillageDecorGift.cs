using RavenNest.DataAnnotations;
using System;

namespace RavenNest.DataModels
{
    /// <summary>
    ///     Decor viewers gave the village with their loyalty points, not placed yet: how many of a piece, and the
    ///     last few givers' names to show in build mode. Placing one takes it from here, removing a gifted piece
    ///     puts it back.
    /// </summary>
    public partial class VillageDecorGift : Entity<VillageDecorGift>
    {
        [PersistentData] private Guid villageId;
        [PersistentData] private string piece;
        [PersistentData] private long amount;

        /// <summary>The last givers' names, newest first, comma separated.</summary>
        [PersistentData] private string givers;
        [PersistentData] private DateTime created;
        [PersistentData] private DateTime? updated;
    }
}

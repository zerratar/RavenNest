using RavenNest.DataAnnotations;
using System;

namespace RavenNest.DataModels
{
    /// <summary>
    /// A road the streamer drew in their village: the points of its centre line, the game draws the
    /// curve and the road through them. Kept apart from the houses, so an older game client never
    /// sees it, and it never takes a building slot.
    /// </summary>
    public partial class VillageRoad : Entity<VillageRoad>
    {
        [PersistentData] private Guid villageId;
        /// <summary>The road type: 0 dirt, 1 gravel, 2 cobbles. See VillageRoadTypes.</summary>
        [PersistentData] private int type;
        /// <summary>The centre line, "x,z;x,z;..." in metres with an invariant decimal point.</summary>
        [PersistentData] private string points;
        /// <summary>
        /// The width at each point, "w;w;..." as multiples of the type's width, one per point. Null or
        /// empty: 1 at every point, as roads had before widths existed.
        /// </summary>
        [PersistentData] private string widths;
        /// <summary>How round each end is: 1 (or null) a half circle, 0 a square end.</summary>
        [PersistentData] private double? startRound;
        [PersistentData] private double? endRound;
        /// <summary>The length of the centre line, in metres.</summary>
        [PersistentData] private double length;
        // What was paid, kept so removing refunds part of the real price, whatever the price list
        // says by then.
        [PersistentData] private double paidWood;
        [PersistentData] private double paidOre;
        [PersistentData] private double paidFish;
        [PersistentData] private double paidWheat;
        [PersistentData] private double paidCoins;
        [PersistentData] private DateTime created;
        [PersistentData] private DateTime? updated;
    }
}

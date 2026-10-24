using RavenNest.DataAnnotations;
using System;

namespace RavenNest.DataModels
{
    /// <summary>
    ///     A change to a village's decor: a piece the streamer placed, or one of the island's own pieces
    ///     (trees, bushes, flowers) that was moved or removed. A village that never touched its decor has
    ///     no rows and looks like the island as shipped.
    /// </summary>
    public partial class VillageDecor : Entity<VillageDecor>
    {
        [PersistentData] private Guid villageId;

        /// <summary>What it is: a key of VillageDecorManager.Pieces.</summary>
        [PersistentData] private string piece;

        /// <summary>The island piece this row changes (VillageDecorDefaults), or empty for a placed piece.</summary>
        [PersistentData] private string defaultKey;

        [PersistentData] private double x;
        [PersistentData] private double y;
        [PersistentData] private double z;
        [PersistentData] private double rotation;
        [PersistentData] private double scale;

        /// <summary>An island piece that was taken away (and paid back for, once).</summary>
        [PersistentData] private bool removed;

        [PersistentData] private double paidWood;
        [PersistentData] private double paidOre;
        [PersistentData] private double paidFish;
        [PersistentData] private double paidWheat;
        [PersistentData] private double paidCoins;
        [PersistentData] private DateTime created;
        [PersistentData] private DateTime? updated;
    }
}

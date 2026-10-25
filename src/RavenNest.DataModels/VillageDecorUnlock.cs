using RavenNest.DataAnnotations;
using System;

namespace RavenNest.DataModels
{
    /// <summary>
    ///     Decor a village has unlocked in a way that is not worked out from its town hall tier and level: a
    ///     seasonal event it streamed during (Key is the event, "christmas"), or a special piece bought once with
    ///     the treasury's coins (Key is the piece). Kept for good.
    /// </summary>
    public partial class VillageDecorUnlock : Entity<VillageDecorUnlock>
    {
        [PersistentData] private Guid villageId;
        [PersistentData] private string key;

        /// <summary>event or treasury.</summary>
        [PersistentData] private string source;
        [PersistentData] private double paidCoins;
        [PersistentData] private DateTime created;
    }
}

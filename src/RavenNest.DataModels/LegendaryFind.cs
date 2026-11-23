using RavenNest.DataAnnotations;
using System;

namespace RavenNest.DataModels
{
    /// <summary>
    ///     A Legendary found (the game's docs/legendary-design.md): who, which, when and from what, the inventory item
    ///     it became, and when it was fully awakened (rank 5). The Hall of Legends and the titles read these. The
    ///     awakening count itself lives on the inventory item's tag, which the game sees.
    /// </summary>
    public partial class LegendaryFind : Entity<LegendaryFind>
    {
        [PersistentData] private Guid characterId;
        [PersistentData] private Guid itemId;
        [PersistentData] private Guid inventoryItemId;
        /// <summary>RavenNest.Combat.LegendarySource, -1 for one given by an admin.</summary>
        [PersistentData] private int source;
        [PersistentData] private DateTime found;
        [PersistentData] private DateTime? awakened;
    }
}

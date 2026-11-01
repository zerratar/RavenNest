using RavenNest.DataAnnotations;
using System;

namespace RavenNest.DataModels
{
    /// <summary>
    ///     A look a player has earned for their boats (the game's docs/own-boats.md, "Customizing boats"): a sail, a flag,
    ///     a figurehead ("sails_7", "flag_3", "figurehead_5"). Per player, for all their characters and boats.
    /// </summary>
    public partial class BoatLookUnlock : Entity<BoatLookUnlock>
    {
        [PersistentData] private Guid userId;
        [PersistentData] private string key;
        /// <summary>What gave it: the regatta, a treasure, a quest.</summary>
        [PersistentData] private string source;
        [PersistentData] private DateTime created;
    }
}

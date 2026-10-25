using RavenNest.DataAnnotations;
using System;

namespace RavenNest.DataModels
{
    /// <summary>
    ///     Something a streamer has earned for good, by a finished story line: an island ("island_mangrove"), a village
    ///     perk ("perk_runestone_nature") or a title for the streamer's own characters ("title_runebreaker").
    /// </summary>
    public partial class StreamerUnlock : Entity<StreamerUnlock>
    {
        [PersistentData] private Guid streamerUserId;
        [PersistentData] private string key;
        /// <summary>The story line that gave it.</summary>
        [PersistentData] private string source;
        [PersistentData] private DateTime created;
    }
}

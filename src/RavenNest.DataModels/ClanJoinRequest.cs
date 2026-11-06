using RavenNest.DataAnnotations;
using System;

namespace RavenNest.DataModels
{
    /// <summary>
    ///     A character asking to join a clan that takes members on request (Clan.JoinPolicy 1), from the clan's page
    ///     or with !clan join; the clan's ranks that may invite accept or decline it on /clan (the game's
    ///     docs/clan-pages-design.md, step 2). Kept after it is decided, so the clan sees who it took and turned away.
    /// </summary>
    public partial class ClanJoinRequest : Entity<ClanJoinRequest>
    {
        [PersistentData] private Guid clanId;
        [PersistentData] private Guid characterId;

        /// <summary>The character's owner, told how it went.</summary>
        [PersistentData] private Guid userId;

        /// <summary>What the asker wrote, up to 200 characters; may be empty.</summary>
        [PersistentData] private string message;

        [PersistentData] private DateTime created;

        /// <summary>0 waiting, 1 accepted, 2 declined, 3 withdrawn (or the character joined a clan another way).</summary>
        [PersistentData] private int state;

        [PersistentData] private Guid? decidedByUserId;
        [PersistentData] private DateTime? decided;
    }
}

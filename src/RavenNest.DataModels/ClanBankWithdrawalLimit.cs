using RavenNest.DataAnnotations;
using System;

namespace RavenNest.DataModels
{
    /// <summary>
    ///     How much one rank may take out of a clan's bank in a day.
    /// </summary>
    /// <remarks>
    ///     A permission on its own is too blunt. "Officers can withdraw" means one bad Officer can
    ///     take everything, and the clan finds out afterwards.
    ///
    ///     <para>
    ///     Keyed by rank level rather than by role id, so a clan renaming or rebuilding its ranks
    ///     does not silently drop its limits and leave the bank open. Level is what the permission
    ///     checks already compare against everywhere else.
    ///     </para>
    /// </remarks>
    public partial class ClanBankWithdrawalLimit : Entity<ClanBankWithdrawalLimit>
    {
        [PersistentData] private Guid clanId;

        /// <summary>The rank this applies to, by <c>ClanRole.Level</c>.</summary>
        [PersistentData] private int roleLevel;

        /// <summary>Items per day. -1 means no limit; 0 means they may not withdraw at all.</summary>
        [PersistentData] private int itemsPerDay;
    }
}

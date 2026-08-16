using RavenNest.DataAnnotations;
using System;

namespace RavenNest.DataModels
{
    /// <summary>
    ///     Who took what out of a clan's bank, or put it in, and when.
    /// </summary>
    /// <remarks>
    ///     Not optional, and not a nice to have added later. A shared bank without a log is a grief
    ///     vector rather than a feature: the first time somebody empties it the clan needs to know
    ///     who, and without this the honest answer is that nobody can tell.
    ///
    ///     <para>
    ///     It is also what makes the per rank daily allowance cheap to enforce, since the allowance
    ///     is a sum over one character's negative rows for today. See
    ///     <see cref="ClanBankWithdrawalLimit"/>.
    ///     </para>
    /// </remarks>
    public partial class ClanBankLog : Entity<ClanBankLog>
    {
        [PersistentData] private Guid clanId;

        /// <summary>The character that did it. A rank belongs to a character, so the log does too.</summary>
        [PersistentData] private Guid characterId;

        [PersistentData] private Guid itemId;

        /// <summary>Negative for a withdrawal, positive for a deposit.</summary>
        [PersistentData] private long amount;

        [PersistentData] private DateTime time;
    }
}

using System;
using RavenNest.DataModels;

namespace RavenNest.BusinessLogic.Game
{
    /// <summary>
    ///     What a rank may take out of the clan bank before anybody has configured anything.
    /// </summary>
    /// <remarks>
    ///     A shared bank with no limits is one bad afternoon from being empty, and a shared bank
    ///     where every limit starts at zero is a feature that appears broken until somebody finds
    ///     the settings. Neither is a good first day, so ranks start with an allowance that matches
    ///     roughly how much a clan already trusts them.
    ///
    ///     <para>
    ///     These are a starting point, not a policy. An owner can change any of them, and nothing
    ///     here ever overwrites a value somebody has set: seeding only fills in ranks that have no
    ///     row at all.
    ///     </para>
    ///
    ///     <para>
    ///     The owner is not in this table. Owning the clan is not a rank, and an owner is not
    ///     limited by one.
    ///     </para>
    /// </remarks>
    public static class ClanBankDefaults
    {
        /// <summary>No limit. Stored rather than implied, so a row can say it out loud.</summary>
        public const int Unlimited = -1;

        /// <summary>
        ///     The daily allowance for a rank, by its level.
        /// </summary>
        /// <remarks>
        ///     The default ranks are Inactive 0, Recruit 1, Member 2 and Officer 3. Anything above
        ///     Officer is a rank the clan invented and put near the top, so it is trusted like one.
        ///
        ///     <para>
        ///     Recruit and Inactive get nothing on purpose. Being able to put things in without
        ///     being able to take them out is the normal shape of joining somewhere, and it is the
        ///     rank a griefer arrives at.
        ///     </para>
        /// </remarks>
        public static int ForRoleLevel(int level)
        {
            if (level <= 1) return 0;      // Inactive, Recruit: deposit only.
            if (level == 2) return 50;     // Member.
            return 500;                    // Officer and anything a clan puts above it.
        }

        public static ClanBankWithdrawalLimit For(Guid clanId, int roleLevel)
        {
            return new ClanBankWithdrawalLimit
            {
                Id = Guid.NewGuid(),
                ClanId = clanId,
                RoleLevel = roleLevel,
                ItemsPerDay = ForRoleLevel(roleLevel)
            };
        }
    }
}

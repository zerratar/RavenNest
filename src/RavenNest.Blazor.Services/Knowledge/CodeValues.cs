using System;
using System.Collections.Generic;
using RavenNest.BusinessLogic;
using RavenNest.BusinessLogic.Game;

namespace RavenNest.Blazor.Services.Knowledge
{
    /// <summary>
    ///     The values a hand written fact is allowed to say it depends on.
    /// </summary>
    /// <remarks>
    ///     A closed list rather than reflection over anything with a name. Two reasons, and the
    ///     second is the important one.
    ///
    ///     <para>
    ///     Reflection by string would silently return nothing for a typo, so a fact could claim to
    ///     be watching a value and be watching nothing at all, which is worse than not watching:
    ///     it looks guarded and is not.
    ///     </para>
    ///
    ///     <para>
    ///     A closed list also means the entries reference the constants in C#, so deleting or
    ///     renaming one breaks the build. The same property the derived facts have, for the same
    ///     reason: the guard cannot outlive the thing it guards.
    ///     </para>
    /// </remarks>
    public static class CodeValues
    {
        private static readonly Dictionary<string, Func<string>> Known =
            new Dictionary<string, Func<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["MaxCharacterCount"] = () => PlayerManager.MaxCharacterCount.ToString(),
                ["AutoJoinDungeonCost"] = () => PlayerManager.AutoJoinDungeonCost.ToString(),
                ["AutoJoinRaidCost"] = () => PlayerManager.AutoJoinRaidCost.ToString(),
                ["AutoRestCostPerSecond"] = () => PlayerManager.AutoRestCostPerSecond.ToString(),
                ["EnchantingCooldownCoinsPerSecond"] = () => PlayerManager.Enchanting_CooldownCoinsPerSecond.ToString(),

                ["MarketplaceListingDays"] = () => ((int)MarketplaceManager.ListingLifetime.TotalDays).ToString(),

                ["VendorBuyMultiplier"] = () => GameMath.VendorBuyMultiplier.ToString(),
                ["MinimumVendorBuyPrice"] = () => GameMath.MinimumVendorBuyPrice.ToString(),
                ["MaxLevel"] = () => GameMath.MaxLevel.ToString(),

                ["MaxPlayerExpMultiplier"] = () => SessionManager.MaxPlayerExpMultiplier.ToString(),
                ["ExpMultiplierMinutesPerScroll"] = () => SessionManager.ExpMultiplierMinutesPerScroll.ToString(),
                ["ExpMultiplierLastTimeMinutes"] = () => SessionManager.ExpMultiplierLastTimeMinutes.ToString(),

                ["ClanBankMemberPerDay"] = () => ClanBankDefaults.ForRoleLevel(2).ToString(),
                ["ClanBankOfficerPerDay"] = () => ClanBankDefaults.ForRoleLevel(3).ToString(),
            };

        /// <summary>Every name a fact may declare a dependency on, for the admin panel to offer.</summary>
        public static IEnumerable<string> Names => Known.Keys;

        /// <summary>
        ///     What the value is now, or null when the name is not one of the known ones.
        /// </summary>
        /// <remarks>
        ///     Null rather than an exception, because an unknown name means a fact written against
        ///     an older list, and that should be ignored rather than take the refresh down with it.
        /// </remarks>
        public static string Read(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            if (!Known.TryGetValue(name, out var read)) return null;

            try
            {
                return read();
            }
            catch
            {
                return null;
            }
        }
    }
}

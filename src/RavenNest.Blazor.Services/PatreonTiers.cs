using System.Collections.Generic;
using System.Linq;
using RavenNest.DataModels;

namespace RavenNest.Blazor.Services
{
    /// <summary>One Patreon tier and what it actually grants.</summary>
    public sealed record PatreonTierInfo(
        int Tier,
        string Name,
        double ExpMultiplier,
        int VillageHouses,
        bool Chatbot);

    /// <summary>
    ///     What each Patreon tier gives, in one place.
    /// </summary>
    /// <remarks>
    ///     Written from the code that grants these rather than from the page that advertised them,
    ///     because the two had drifted:
    ///
    ///     <list type="bullet">
    ///     <item>The experience multiplier is the game client's TwitchEventManager.TierExpMultis,
    ///     which is { 0, 2, 3, 5, 5, ... } by tier. The dashboard page listed no experience perk at
    ///     all for Rune, which actually gets 3x, and listed Dragon as 3x when it gets 5x.</item>
    ///     <item>The house floors are GameData.GetOrCreateVillageHouses: 10 at Rune, 20 at Dragon,
    ///     30 at Abraxas, and nothing above that. The page claimed the top tier unlocked all of
    ///     them, which would be 40.</item>
    ///     <item>Village experience doubles from Mithril up, in VillageProcessor.</item>
    ///     </list>
    ///
    ///     Two pages describing the same thing from separate lists is how that happened, so there
    ///     is one list now and both read it.
    /// </remarks>
    public static class PatreonTiers
    {
        /// <summary>Village experience doubles from this tier upwards.</summary>
        public const Patreon DoubleVillageExpFrom = Patreon.Mithril;

        /// <summary>The most plots any town can hold, however it got them.</summary>
        public const int MaxVillageHouses = 40;

        public static readonly PatreonTierInfo[] All = new[]
        {
            new PatreonTierInfo(1, "Mithril", 2, 0, false),
            new PatreonTierInfo(2, "Rune", 3, 10, false),
            new PatreonTierInfo(3, "Dragon", 5, 20, true),
            new PatreonTierInfo(4, "Abraxas", 5, 30, true),
            new PatreonTierInfo(5, "Phantom", 5, 30, true),
        };

        public static PatreonTierInfo Get(int tier) => All.FirstOrDefault(x => x.Tier == tier);

        /// <summary>
        ///     What every tier gets, so it is stated once rather than repeated on each card.
        /// </summary>
        public static readonly IReadOnlyList<string> SharedPerks = new List<string>
        {
            "Found a clan and invite characters to it",
            "Automatic item use in game, which is the !use command running on its own",
            "Double town experience, so your town levels twice as fast",
        };
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Settings;

namespace RavenNest.BusinessLogic.Game
{
    public sealed class MarketPrice
    {
        /// <summary>The middle of what people actually paid, in coins per item.</summary>
        public long Median { get; set; }

        /// <summary>How many sales went into it after the guards below had their say.</summary>
        public int Sample { get; set; }

        /// <summary>How many different people were selling. Two is the minimum to count.</summary>
        public int Sellers { get; set; }
    }

    /// <summary>
    ///     What players actually pay each other, per item.
    /// </summary>
    /// <remarks>
    ///     The vendor's asking price used to come only from crafting ingredient costs, which is a
    ///     number about how an item is made rather than about what it is worth. Where those two
    ///     disagree the vendor undercuts the marketplace, and a shop that is permanently cheaper
    ///     than the players is a shop that replaces them.
    ///
    ///     <para>
    ///     Nothing is stored. The source is <c>MarketItemTransaction</c>, which is already persisted
    ///     and already held in memory, so the index is rebuilt from it rather than kept alongside it.
    ///     That rules out the failure where a cached price and the trades it came from disagree, and
    ///     it means no schema change. The recompute walks a month of sales, which is a few hundred
    ///     rows.
    ///     </para>
    ///
    ///     <para>
    ///     Only the buy side moves. What the vendor pays is left exactly as it was, so this can only
    ///     ever raise the gap between buying and selling, never close it. The no profitable round
    ///     trip guarantee that was checked exhaustively when the 3x rule went in therefore still
    ///     holds by construction, rather than needing to be rechecked against every price this
    ///     produces.
    ///     </para>
    ///
    ///     <para>
    ///     That one sidedness is also why the guards here can stay light. A price talked upwards
    ///     only makes the vendor expensive for that item, which costs nobody anything, and a price
    ///     talked downwards cannot go below the formula floor because the caller takes the larger of
    ///     the two. The guards exist to stop one person's opinion being mistaken for a market, not
    ///     to stop an attack that pays.
    ///     </para>
    /// </remarks>
    public class MarketPriceIndex
    {
        /// <summary>
        ///     How far back the sales are read: everything the transaction table keeps.
        /// </summary>
        /// <remarks>
        ///     Thirty days was the first choice, for freshness. Prod holds 651 marketplace sales in
        ///     total, about eleven a day for the whole game, so halving the sample to get a fresher
        ///     number would mean most items never clearing a floor at all. With trade this thin the
        ///     sample is the scarce thing, not the recency.
        /// </remarks>
        private static readonly TimeSpan Window = TimeSpan.FromDays(60);

        /// <summary>
        ///     How long a built index is used before it is rebuilt. Prices from player trades move
        ///     over days, not minutes.
        /// </summary>
        private static readonly TimeSpan Freshness = TimeSpan.FromMinutes(30);

        /// <summary>
        ///     Fewest sales an item needs before its price means anything.
        /// </summary>
        /// <remarks>
        ///     Provisional. The whole game trades on the order of ten items a day, so most items
        ///     will never clear this and will keep the formula price, which is the right outcome.
        ///     The exact number wants setting from the real distribution rather than from taste.
        /// </remarks>
        private const int MinimumSales = 3;

        /// <summary>
        ///     Fewest different sellers. One person selling the same thing five times is that
        ///     person's asking price, not a market price.
        /// </summary>
        private const int MinimumSellers = 2;

        /// <summary>
        ///     How many of one seller's sales count towards an item. Beyond this their extra sales
        ///     are ignored, which caps how far any one person can pull the middle without needing a
        ///     percentage rule that behaves oddly on small samples.
        /// </summary>
        private const int MaxSalesPerSeller = 3;

        private readonly GameData gameData;
        private readonly IServerSettingsProvider settings;
        private readonly ILogger<MarketPriceIndex> logger;
        private readonly object mutex = new object();

        private Dictionary<Guid, MarketPrice> prices = new Dictionary<Guid, MarketPrice>();
        private DateTime builtUtc = DateTime.MinValue;

        public MarketPriceIndex(
            GameData gameData,
            IServerSettingsProvider settings,
            ILogger<MarketPriceIndex> logger)
        {
            this.gameData = gameData;
            this.settings = settings;
            this.logger = logger;
        }

        /// <summary>
        ///     Whether the vendor is allowed to act on any of this yet.
        /// </summary>
        /// <remarks>
        ///     Off by default, and only the acting is gated: the prices are still computed and still
        ///     readable while it is off, so what would happen can be looked at on the vendor page
        ///     before it happens to anybody. Turning it on raises prices on some items, and a price
        ///     rise that arrives without being announced reads as a bug.
        /// </remarks>
        public bool IsEnabled => settings.GetToggle(ServerSettingsRegistry.VendorFollowMarket);

        /// <summary>
        ///     What the item goes for, or null when there is not enough evidence to say.
        /// </summary>
        public MarketPrice Get(Guid itemId)
        {
            var current = Current();
            return current.TryGetValue(itemId, out var price) ? price : null;
        }

        /// <summary>
        ///     The number to hand to the vendor price calculation. Zero means no opinion, which
        ///     leaves the formula price alone.
        /// </summary>
        /// <remarks>
        ///     The single door between the readings and the till. Everything that prices anything
        ///     goes through here rather than reading <see cref="Get"/> and using the median itself,
        ///     so the switch cannot be honoured in one place and missed in another.
        /// </remarks>
        public long GetAnchor(Guid itemId) => IsEnabled ? Get(itemId)?.Median ?? 0 : 0;

        public IReadOnlyDictionary<Guid, MarketPrice> All() => Current();

        private Dictionary<Guid, MarketPrice> Current()
        {
            lock (mutex)
            {
                if (DateTime.UtcNow - builtUtc < Freshness)
                {
                    return prices;
                }

                try
                {
                    prices = Build();
                }
                catch (Exception exc)
                {
                    // A failed rebuild keeps the previous index rather than emptying it. An empty
                    // index would silently drop every price back to the formula, which looks like
                    // a deliberate repricing and is not.
                    logger.LogError("Could not rebuild the market price index: " + exc);
                }

                builtUtc = DateTime.UtcNow;
                return prices;
            }
        }

        private Dictionary<Guid, MarketPrice> Build()
        {
            var now = DateTime.UtcNow;
            var sales = gameData.GetMarketItemTransactions(now - Window, now);
            var byItem = new Dictionary<Guid, List<Sale>>();

            foreach (var sale in sales)
            {
                if (sale.Amount <= 0 || sale.PricePerItem <= 0) continue;

                var seller = UserOf(sale.SellerCharacterId);
                var buyer = UserOf(sale.BuyerCharacterId);

                // Both sides the same person. Compared by user rather than by character, because
                // moving an item between your own characters and paying yourself for it is the
                // cheapest way there is to make a price look real.
                if (seller != Guid.Empty && seller == buyer) continue;

                if (!byItem.TryGetValue(sale.ItemId, out var list))
                {
                    byItem[sale.ItemId] = list = new List<Sale>();
                }

                list.Add(new Sale { Seller = seller, Price = sale.PricePerItem });
            }

            var built = new Dictionary<Guid, MarketPrice>();

            foreach (var pair in byItem)
            {
                var kept = Cap(pair.Value);
                if (kept.Count < MinimumSales) continue;

                var sellers = kept.Select(x => x.Seller).Distinct().Count();
                if (sellers < MinimumSellers) continue;

                built[pair.Key] = new MarketPrice
                {
                    Median = Median(kept.Select(x => x.Price)),
                    Sample = kept.Count,
                    Sellers = sellers
                };
            }

            return built;
        }

        /// <summary>
        ///     Keeps at most <see cref="MaxSalesPerSeller"/> sales from any one seller, cheapest
        ///     first so that a seller cannot buy influence by repeating their dearest sale.
        /// </summary>
        private static List<Sale> Cap(List<Sale> sales)
        {
            return sales
                .GroupBy(x => x.Seller)
                .SelectMany(g => g.OrderBy(x => x.Price).Take(MaxSalesPerSeller))
                .ToList();
        }

        private static long Median(IEnumerable<double> values)
        {
            var sorted = values.OrderBy(x => x).ToList();
            if (sorted.Count == 0) return 0;

            var middle = sorted.Count / 2;
            var median = sorted.Count % 2 == 1
                ? sorted[middle]
                : (sorted[middle - 1] + sorted[middle]) / 2d;

            return (long)Math.Truncate(median);
        }

        private Guid UserOf(Guid characterId)
        {
            return gameData.GetCharacter(characterId)?.UserId ?? Guid.Empty;
        }

        private struct Sale
        {
            public Guid Seller;
            public double Price;
        }
    }
}

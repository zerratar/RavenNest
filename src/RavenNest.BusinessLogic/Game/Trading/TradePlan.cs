using System;
using System.Collections.Generic;
using System.Linq;

namespace RavenNest.BusinessLogic.Game.Trading
{
    public enum TradeKind
    {
        MarketplacePurchase,
        MarketplaceListing,
        MarketplaceCancel,
        VendorSale,
        VendorPurchase,
        Gift,
        StashDeposit,
        StashWithdraw,
        ClanBankDeposit,
        ClanBankWithdraw,
        AdminAdjustment
    }

    /// <summary>
    ///     Moving a quantity of one stack from one holder to another.
    /// </summary>
    /// <remarks>
    ///     <see cref="Outside"/> on either side means the items come from or go to somewhere that is
    ///     not a holder we track: a vendor, a market listing, a loot drop, an administrator's hand.
    ///     That one convention is what lets a vendor sale and a marketplace purchase share an
    ///     executor instead of each growing their own.
    /// </remarks>
    public readonly struct ItemMove
    {
        /// <summary>Outside the world. Never runs out and never overflows.</summary>
        public static readonly Guid Outside = Guid.Empty;

        public readonly Guid From;
        public readonly Guid To;
        public readonly StackKey Stack;
        public readonly long Amount;

        public ItemMove(Guid from, Guid to, StackKey stack, long amount)
        {
            From = from;
            To = to;
            Stack = stack;
            Amount = amount;
        }

        public override string ToString() =>
            Amount + "x " + Stack.ItemId + " " + Describe(From) + " to " + Describe(To);

        internal static string Describe(Guid holder) => holder == Outside ? "outside" : holder.ToString();
    }

    public readonly struct CoinMove
    {
        public readonly Guid From;
        public readonly Guid To;
        public readonly long Amount;

        public CoinMove(Guid from, Guid to, long amount)
        {
            From = from;
            To = to;
            Amount = amount;
        }

        public override string ToString() =>
            Amount + " coins " + ItemMove.Describe(From) + " to " + ItemMove.Describe(To);
    }

    /// <summary>
    ///     What a trade would do, decided before anything is touched.
    /// </summary>
    /// <remarks>
    ///     Produced by a pure function from the state of the world, so the deciding can be tested
    ///     without a database and read without following four levels of loop. Nothing here has
    ///     happened; a plan is a proposal until <c>TradeExecutor.Commit</c> is called on it.
    /// </remarks>
    public sealed class TradePlan
    {
        public TradePlan(TradeKind kind, IReadOnlyList<ItemMove> items = null, IReadOnlyList<CoinMove> coins = null)
        {
            Kind = kind;
            Items = items ?? Array.Empty<ItemMove>();
            Coins = coins ?? Array.Empty<CoinMove>();
        }

        public TradeKind Kind { get; }

        public IReadOnlyList<ItemMove> Items { get; }

        public IReadOnlyList<CoinMove> Coins { get; }

        public bool IsEmpty => Items.Count == 0 && Coins.Count == 0;

        /// <summary>
        ///     Everyone the plan touches, for locking and for logging. Outside is not a holder.
        /// </summary>
        public IReadOnlyList<Guid> Holders =>
            Items.SelectMany(x => new[] { x.From, x.To })
                .Concat(Coins.SelectMany(x => new[] { x.From, x.To }))
                .Where(x => x != ItemMove.Outside)
                .Distinct()
                .ToList();

        public override string ToString() =>
            Kind + ": " + string.Join("; ", Items.Select(x => x.ToString()).Concat(Coins.Select(x => x.ToString())));
    }

    public sealed class TradeResult
    {
        private TradeResult(bool ok, string reason)
        {
            Ok = ok;
            Reason = reason;
        }

        public bool Ok { get; }

        /// <summary>Why it did not happen, written for the person who tried.</summary>
        public string Reason { get; }

        public static readonly TradeResult Success = new TradeResult(true, null);

        public static TradeResult Failed(string reason) => new TradeResult(false, reason);
    }
}

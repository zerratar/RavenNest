using System;
using System.Collections.Generic;
using System.Linq;

namespace RavenNest.BusinessLogic.Game
{
    /// <summary>One listing, as far as planning a purchase is concerned.</summary>
    public readonly struct MarketListing
    {
        public readonly Guid Id;
        public readonly Guid SellerCharacterId;
        public readonly long Amount;
        public readonly double PricePerItem;

        public MarketListing(Guid id, Guid sellerCharacterId, long amount, double pricePerItem)
        {
            Id = id;
            SellerCharacterId = sellerCharacterId;
            Amount = amount;
            PricePerItem = pricePerItem;
        }
    }

    /// <summary>How much to take from one listing.</summary>
    public readonly struct MarketAllocation
    {
        public readonly Guid MarketItemId;
        public readonly Guid SellerCharacterId;
        public readonly long Amount;
        public readonly double PricePerItem;

        public MarketAllocation(Guid marketItemId, Guid sellerCharacterId, long amount, double pricePerItem)
        {
            MarketItemId = marketItemId;
            SellerCharacterId = sellerCharacterId;
            Amount = amount;
            PricePerItem = pricePerItem;
        }

        public double TotalCost => Amount * PricePerItem;
    }

    /// <summary>
    ///     Why a plan is the size it is. A partial fill is data now rather than a consequence of
    ///     where a loop happened to break.
    /// </summary>
    public enum PurchasePlanOutcome
    {
        /// <summary>The whole requested amount is covered.</summary>
        Filled,

        /// <summary>Some of it is covered. The reason it stopped is one of the three below.</summary>
        PartiallyFilled,

        /// <summary>Nothing is listed, or every listing is empty.</summary>
        NothingAvailable,

        /// <summary>There is stock, but not a single unit is affordable.</summary>
        InsufficientCoins,

        /// <summary>There is stock and money, but nothing can be bought without breaking the price cap.</summary>
        PriceTooHigh,

        /// <summary>The request itself was not a request: a non positive amount or cap.</summary>
        RequestTooLow
    }

    public sealed class MarketPurchasePlan
    {
        public static readonly MarketPurchasePlan Empty =
            new MarketPurchasePlan(Array.Empty<MarketAllocation>(), PurchasePlanOutcome.NothingAvailable);

        public MarketPurchasePlan(IReadOnlyList<MarketAllocation> allocations, PurchasePlanOutcome outcome)
        {
            Allocations = allocations ?? Array.Empty<MarketAllocation>();
            Outcome = outcome;
            TotalAmount = Allocations.Sum(x => x.Amount);
            TotalCost = Allocations.Sum(x => x.TotalCost);
        }

        public IReadOnlyList<MarketAllocation> Allocations { get; }
        public PurchasePlanOutcome Outcome { get; }
        public long TotalAmount { get; }
        public double TotalCost { get; }

        public bool IsEmpty => TotalAmount <= 0;

        /// <summary>The price the buyer actually ends up paying per item, across the whole plan.</summary>
        public double AveragePricePerItem => TotalAmount <= 0 ? 0 : TotalCost / TotalAmount;
    }

    /// <summary>
    ///     Decides what a marketplace purchase should take from which listings.
    ///
    ///     Pure: no data layer, no session, no mutation, nothing to mock. That is the point. The
    ///     loop this replaces interleaved selection, affordability, mutation and reporting, under a
    ///     comment reading "todo(zerratar): Rewrite this!! This is horrible", and the allocation
    ///     rule the rest of that comment describes was never actually implemented.
    /// </summary>
    /// <remarks>
    ///     Three things the old loop got wrong, all of which fall out of separating the decision
    ///     from the doing.
    ///
    ///     <para>
    ///     <b>It was not buying the cheapest.</b> The comment says it buys "by the cheapest items".
    ///     It walked <c>GetMarketItems</c>, which returns listings in insertion order, and never
    ///     sorted. So which listings you got depended on the order they happened to be created in,
    ///     and a buyer could pay more than the market was asking while cheaper stock sat there.
    ///     </para>
    ///
    ///     <para>
    ///     <b>It never spent the money.</b> The buyer's coin balance was read once before the loop
    ///     and never reduced as listings were taken, so every listing was checked for affordability
    ///     against the full original balance. Buying across three listings planned to spend money
    ///     three times over. It did not overdraw anybody, because the per listing commit re-checked
    ///     the balance and returned zero, but that turned an arithmetic error into a silent partial
    ///     fill with no reason attached.
    ///     </para>
    ///
    ///     <para>
    ///     <b>One stale listing ended the purchase.</b> A listing with an amount of zero hit a
    ///     <c>break</c> rather than a <c>continue</c>, so it stopped the whole order rather than
    ///     being skipped. Empty listings are simply not candidates here.
    ///     </para>
    /// </remarks>
    public static class MarketPurchasePlanner
    {
        /// <summary>
        ///     Prices are doubles, so the average test needs a tolerance or a purchase that lands
        ///     exactly on the cap fails on a representation error.
        /// </summary>
        private const double Epsilon = 1e-9;

        /// <summary>
        ///     Cheapest first, and then over the cap only while the average stays under it.
        ///
        ///     That second half is the rule the old comment described and the old code did not do:
        ///     it skipped anything priced above the cap outright, so an order that could have been
        ///     filled at an acceptable average price came back short instead.
        ///
        ///     Cheapest first is also what makes this optimal rather than merely reasonable. Taking
        ///     the cheapest unit available at every step both maximises how many units a budget
        ///     buys and minimises the running average, so if this cannot add another unit within
        ///     the cap then no other selection of that size could either.
        /// </summary>
        public static MarketPurchasePlan Plan(
            IReadOnlyList<MarketListing> listings,
            long requestedAmount,
            double maxPricePerItem,
            double availableCoins)
        {
            if (requestedAmount <= 0 || maxPricePerItem <= 0)
            {
                return new MarketPurchasePlan(Array.Empty<MarketAllocation>(), PurchasePlanOutcome.RequestTooLow);
            }

            var candidates = (listings ?? Array.Empty<MarketListing>())
                .Where(x => x.Amount > 0 && x.PricePerItem > 0)
                // Id is the tie break so that two listings at the same price always plan the same
                // way. Without it the plan depends on enumeration order, which makes it untestable
                // and makes a re-plan between validate and commit look like a change.
                .OrderBy(x => x.PricePerItem)
                .ThenBy(x => x.Id)
                .ToList();

            if (candidates.Count == 0)
            {
                return new MarketPurchasePlan(Array.Empty<MarketAllocation>(), PurchasePlanOutcome.NothingAvailable);
            }

            var allocations = new List<MarketAllocation>();
            var remaining = requestedAmount;
            var spent = 0d;
            var bought = 0L;

            // Which constraint stopped it, for the outcome when the order comes back short. Coins
            // are checked before the cap on each listing, so a run that hits both reports coins,
            // which is the one the buyer can do something about.
            var blockedByCoins = false;
            var blockedByPrice = false;

            foreach (var listing in candidates)
            {
                if (remaining <= 0)
                {
                    break;
                }

                var take = Math.Min(remaining, listing.Amount);

                var affordable = AffordableUnits(listing.PricePerItem, availableCoins - spent);
                if (affordable < take)
                {
                    blockedByCoins = true;
                    take = affordable;
                }

                var withinCap = UnitsWithinAverageCap(listing.PricePerItem, maxPricePerItem, spent, bought);
                if (withinCap < take)
                {
                    blockedByPrice = true;
                    take = withinCap;
                }

                if (take <= 0)
                {
                    // A cheaper listing could not be afforded or could not stay under the cap, and
                    // everything after this one is dearer, so neither can they.
                    break;
                }

                allocations.Add(new MarketAllocation(listing.Id, listing.SellerCharacterId, take, listing.PricePerItem));
                spent += take * listing.PricePerItem;
                bought += take;
                remaining -= take;
            }

            if (bought >= requestedAmount)
            {
                return new MarketPurchasePlan(allocations, PurchasePlanOutcome.Filled);
            }

            if (bought > 0)
            {
                return new MarketPurchasePlan(allocations, PurchasePlanOutcome.PartiallyFilled);
            }

            if (blockedByCoins)
            {
                return new MarketPurchasePlan(allocations, PurchasePlanOutcome.InsufficientCoins);
            }

            if (blockedByPrice)
            {
                return new MarketPurchasePlan(allocations, PurchasePlanOutcome.PriceTooHigh);
            }

            return new MarketPurchasePlan(allocations, PurchasePlanOutcome.NothingAvailable);
        }

        /// <summary>How many units of a given price fit in what is left of the budget.</summary>
        private static long AffordableUnits(double pricePerItem, double coinsLeft)
        {
            if (coinsLeft <= 0)
            {
                return 0;
            }

            var units = Math.Floor((coinsLeft + Epsilon) / pricePerItem);
            return units <= 0 ? 0 : (long)Math.Min(units, long.MaxValue);
        }

        /// <summary>
        ///     How many units at this price can be added before the average across the whole
        ///     purchase would exceed the cap.
        ///
        ///     Below the cap the answer is unbounded: the average starts at or under the cap and
        ///     adding something cheaper than the cap cannot push it over.
        ///
        ///     Above it, the average rises with every unit, and the largest k that keeps it legal
        ///     comes straight out of the inequality:
        ///
        ///         (spent + k*price) / (bought + k) &lt;= cap
        ///         spent + k*price &lt;= cap*bought + cap*k
        ///         k*(price - cap) &lt;= cap*bought - spent
        ///         k &lt;= (cap*bought - spent) / (price - cap)
        ///
        ///     which is why buying cheaply first is not just tidier: it is what builds the headroom
        ///     that lets a dearer listing be used at all.
        /// </summary>
        private static long UnitsWithinAverageCap(double pricePerItem, double cap, double spent, long bought)
        {
            if (pricePerItem <= cap + Epsilon)
            {
                return long.MaxValue;
            }

            var headroom = (cap * bought) - spent;
            if (headroom <= 0)
            {
                return 0;
            }

            var units = Math.Floor((headroom + Epsilon) / (pricePerItem - cap));
            return units <= 0 ? 0 : (long)Math.Min(units, long.MaxValue);
        }
    }
}

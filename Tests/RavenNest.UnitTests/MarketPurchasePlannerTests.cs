using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RavenNest.BusinessLogic.Game;

namespace RavenNest.UnitTests
{
    /// <summary>
    ///     The planner is a pure function, which is the whole reason it was pulled out of the
    ///     purchase loop: none of this needs a database, a session or a mock.
    /// </summary>
    [TestClass]
    public class MarketPurchasePlannerTests
    {
        private static int nextId;

        private static MarketListing Listing(long amount, double price)
        {
            // Deterministic ids so the tie break between equally priced listings is stable and
            // the assertions can name which listing was taken from.
            var id = new Guid(++nextId, 0, 0, new byte[8]);
            return new MarketListing(id, Guid.NewGuid(), amount, price);
        }

        private static List<MarketListing> Listings(params MarketListing[] listings) => listings.ToList();

        [TestInitialize]
        public void Reset() => nextId = 0;

        // ---- The basics -----------------------------------------------------

        [TestMethod]
        public void TakesEverythingFromOneListingWhenItCovers()
        {
            var plan = MarketPurchasePlanner.Plan(Listings(Listing(10, 5)), 4, 10, 1000);

            Assert.AreEqual(PurchasePlanOutcome.Filled, plan.Outcome);
            Assert.AreEqual(1, plan.Allocations.Count);
            Assert.AreEqual(4, plan.TotalAmount);
            Assert.AreEqual(20, plan.TotalCost, 1e-9);
        }

        [TestMethod]
        public void SpreadsAcrossListingsWhenOneIsNotEnough()
        {
            var plan = MarketPurchasePlanner.Plan(
                Listings(Listing(3, 5), Listing(3, 6), Listing(3, 7)), 7, 10, 1000);

            Assert.AreEqual(PurchasePlanOutcome.Filled, plan.Outcome);
            Assert.AreEqual(7, plan.TotalAmount);
            Assert.AreEqual(3 * 5 + 3 * 6 + 1 * 7, plan.TotalCost, 1e-9);
        }

        /// <summary>
        ///     The old loop walked the listings in the order the data layer returned them, which is
        ///     insertion order, and never sorted. So it could buy the dear stock and leave the
        ///     cheap stock sitting there.
        /// </summary>
        [TestMethod]
        public void BuysTheCheapestFirstWhateverOrderTheListingsArriveIn()
        {
            var plan = MarketPurchasePlanner.Plan(
                Listings(Listing(5, 100), Listing(5, 1), Listing(5, 50)), 5, 1000, 100000);

            Assert.AreEqual(PurchasePlanOutcome.Filled, plan.Outcome);
            Assert.AreEqual(1, plan.Allocations.Count);
            Assert.AreEqual(1, plan.Allocations[0].PricePerItem, 1e-9);
            Assert.AreEqual(5, plan.TotalCost, 1e-9);
        }

        // ---- Money ----------------------------------------------------------

        /// <summary>
        ///     The balance was read once before the old loop and never reduced, so affordability
        ///     was tested against the full amount for every listing in turn. Ten coins would plan
        ///     to buy ten coins' worth from each of three listings.
        /// </summary>
        [TestMethod]
        public void SpendsTheCoinsItHasAlreadyCommitted()
        {
            var plan = MarketPurchasePlanner.Plan(
                Listings(Listing(5, 2), Listing(5, 2), Listing(5, 2)), 15, 10, 10);

            Assert.AreEqual(PurchasePlanOutcome.PartiallyFilled, plan.Outcome);
            Assert.AreEqual(5, plan.TotalAmount);
            Assert.AreEqual(10, plan.TotalCost, 1e-9);
            Assert.IsTrue(plan.TotalCost <= 10);
        }

        [TestMethod]
        public void BuysWhatItCanAffordAndSaysSo()
        {
            var plan = MarketPurchasePlanner.Plan(Listings(Listing(10, 3)), 10, 10, 10);

            Assert.AreEqual(PurchasePlanOutcome.PartiallyFilled, plan.Outcome);
            Assert.AreEqual(3, plan.TotalAmount);
            Assert.AreEqual(9, plan.TotalCost, 1e-9);
        }

        [TestMethod]
        public void NoCoinsBuysNothingAndSaysWhy()
        {
            var plan = MarketPurchasePlanner.Plan(Listings(Listing(10, 3)), 10, 10, 1);

            Assert.AreEqual(PurchasePlanOutcome.InsufficientCoins, plan.Outcome);
            Assert.AreEqual(0, plan.TotalAmount);
            Assert.IsTrue(plan.IsEmpty);
        }

        // ---- The average price rule ----------------------------------------

        /// <summary>
        ///     The rule the old comment described and the old code never implemented. It skipped
        ///     anything priced above the cap outright, so this order came back short.
        /// </summary>
        [TestMethod]
        public void GoesOverTheCapWhileTheAverageStaysUnderIt()
        {
            // Five at 5 and five at 15, cap 10. Buying all ten averages exactly 10.
            var plan = MarketPurchasePlanner.Plan(
                Listings(Listing(5, 5), Listing(5, 15)), 10, 10, 100000);

            Assert.AreEqual(PurchasePlanOutcome.Filled, plan.Outcome);
            Assert.AreEqual(10, plan.TotalAmount);
            Assert.AreEqual(100, plan.TotalCost, 1e-9);
            Assert.AreEqual(10, plan.AveragePricePerItem, 1e-9);
        }

        [TestMethod]
        public void StopsAtThePointTheAverageWouldBreakTheCap()
        {
            // Five at 5 gives 25 spent over 5 units, 25 of headroom against a cap of 10.
            // Each unit at 15 costs 5 of that headroom, so exactly five more fit.
            var plan = MarketPurchasePlanner.Plan(
                Listings(Listing(5, 5), Listing(50, 15)), 50, 10, 1000000);

            Assert.AreEqual(PurchasePlanOutcome.PartiallyFilled, plan.Outcome);
            Assert.AreEqual(10, plan.TotalAmount);
            Assert.IsTrue(plan.AveragePricePerItem <= 10 + 1e-9);
        }

        [TestMethod]
        public void WillNotStartAboveTheCapWithNoHeadroom()
        {
            var plan = MarketPurchasePlanner.Plan(Listings(Listing(10, 15)), 10, 10, 100000);

            Assert.AreEqual(PurchasePlanOutcome.PriceTooHigh, plan.Outcome);
            Assert.AreEqual(0, plan.TotalAmount);
        }

        [TestMethod]
        public void APurchaseLandingExactlyOnTheCapIsAllowed()
        {
            var plan = MarketPurchasePlanner.Plan(Listings(Listing(10, 10)), 10, 10, 100000);

            Assert.AreEqual(PurchasePlanOutcome.Filled, plan.Outcome);
            Assert.AreEqual(10, plan.TotalAmount);
            Assert.AreEqual(10, plan.AveragePricePerItem, 1e-9);
        }

        /// <summary>
        ///     The property that makes cheapest first the right rule rather than merely a tidy one:
        ///     whatever the plan buys, it never pays more on average than the buyer asked to.
        /// </summary>
        [TestMethod]
        public void NeverExceedsTheCapOnAverage()
        {
            var random = new Random(20260816);

            for (var run = 0; run < 2000; ++run)
            {
                nextId = 0;
                var listings = new List<MarketListing>();
                var count = random.Next(1, 8);
                for (var i = 0; i < count; ++i)
                {
                    listings.Add(Listing(random.Next(1, 20), random.Next(1, 40)));
                }

                var requested = random.Next(1, 60);
                var cap = random.Next(1, 40);
                var coins = random.Next(0, 800);

                var plan = MarketPurchasePlanner.Plan(listings, requested, cap, coins);

                Assert.IsTrue(plan.TotalAmount <= requested, "bought more than was asked for");
                Assert.IsTrue(plan.TotalCost <= coins + 1e-9, "spent more than the buyer had");

                if (plan.TotalAmount > 0)
                {
                    Assert.IsTrue(plan.AveragePricePerItem <= cap + 1e-9,
                        $"average {plan.AveragePricePerItem} broke cap {cap}");
                }

                foreach (var allocation in plan.Allocations)
                {
                    var source = listings.First(x => x.Id == allocation.MarketItemId);
                    Assert.IsTrue(allocation.Amount <= source.Amount, "took more than the listing held");
                    Assert.IsTrue(allocation.Amount > 0, "planned a zero allocation");
                }

                Assert.AreEqual(plan.Allocations.Count, plan.Allocations.Select(x => x.MarketItemId).Distinct().Count(),
                    "planned the same listing twice");
            }
        }

        // ---- Things that used to derail it ---------------------------------

        /// <summary>
        ///     An empty listing hit a break rather than a continue, so one stale row stopped the
        ///     rest of the order.
        /// </summary>
        [TestMethod]
        public void AnEmptyListingDoesNotStopTheOrder()
        {
            var plan = MarketPurchasePlanner.Plan(
                Listings(Listing(0, 1), Listing(10, 5)), 5, 10, 1000);

            Assert.AreEqual(PurchasePlanOutcome.Filled, plan.Outcome);
            Assert.AreEqual(5, plan.TotalAmount);
            Assert.AreEqual(25, plan.TotalCost, 1e-9);
        }

        [TestMethod]
        public void FreeListingsAreNotCandidates()
        {
            var plan = MarketPurchasePlanner.Plan(Listings(Listing(10, 0)), 5, 10, 1000);

            Assert.AreEqual(PurchasePlanOutcome.NothingAvailable, plan.Outcome);
            Assert.AreEqual(0, plan.TotalAmount);
        }

        [TestMethod]
        public void NothingListedIsNotAFailure()
        {
            var plan = MarketPurchasePlanner.Plan(new List<MarketListing>(), 5, 10, 1000);

            Assert.AreEqual(PurchasePlanOutcome.NothingAvailable, plan.Outcome);
            Assert.IsTrue(plan.IsEmpty);
        }

        [TestMethod]
        public void NullListingsAreNotACrash()
        {
            var plan = MarketPurchasePlanner.Plan(null, 5, 10, 1000);

            Assert.AreEqual(PurchasePlanOutcome.NothingAvailable, plan.Outcome);
        }

        [TestMethod]
        public void ANonRequestIsRejectedBeforeAnythingElse()
        {
            Assert.AreEqual(PurchasePlanOutcome.RequestTooLow,
                MarketPurchasePlanner.Plan(Listings(Listing(10, 1)), 0, 10, 1000).Outcome);

            Assert.AreEqual(PurchasePlanOutcome.RequestTooLow,
                MarketPurchasePlanner.Plan(Listings(Listing(10, 1)), -5, 10, 1000).Outcome);

            Assert.AreEqual(PurchasePlanOutcome.RequestTooLow,
                MarketPurchasePlanner.Plan(Listings(Listing(10, 1)), 5, 0, 1000).Outcome);
        }

        /// <summary>
        ///     Two listings at the same price have to plan the same way every time, or a re-plan
        ///     between validate and commit looks like the market moved when it did not.
        /// </summary>
        [TestMethod]
        public void EquallyPricedListingsPlanTheSameWayEveryTime()
        {
            nextId = 0;
            var listings = Listings(Listing(5, 7), Listing(5, 7), Listing(5, 7));

            var first = MarketPurchasePlanner.Plan(listings, 7, 10, 10000);
            var reversed = MarketPurchasePlanner.Plan(Enumerable.Reverse(listings).ToList(), 7, 10, 10000);

            CollectionAssert.AreEqual(
                first.Allocations.Select(x => x.MarketItemId).ToList(),
                reversed.Allocations.Select(x => x.MarketItemId).ToList());

            CollectionAssert.AreEqual(
                first.Allocations.Select(x => x.Amount).ToList(),
                reversed.Allocations.Select(x => x.Amount).ToList());
        }

        [TestMethod]
        public void NeverPlansMoreThanWasAskedFor()
        {
            var plan = MarketPurchasePlanner.Plan(
                Listings(Listing(1000, 1), Listing(1000, 1)), 3, 10, 100000);

            Assert.AreEqual(3, plan.TotalAmount);
            Assert.AreEqual(PurchasePlanOutcome.Filled, plan.Outcome);
        }
    }
}

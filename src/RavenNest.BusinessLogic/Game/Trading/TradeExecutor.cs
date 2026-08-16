using System;
using System.Collections.Generic;

namespace RavenNest.BusinessLogic.Game.Trading
{
    /// <summary>
    ///     The bit of the world a trade can touch.
    /// </summary>
    /// <remarks>
    ///     Narrow on purpose. Everything the executor needs is here, so it can be run against a
    ///     fake and its rollback proved without a database, and so the list of things a trade is
    ///     able to do is short enough to read.
    ///
    ///     <para>
    ///     Implementations must treat <see cref="ItemMove.Outside"/> as bottomless in both
    ///     directions: taking from it always succeeds and giving to it always succeeds. That is
    ///     what a vendor is.
    ///     </para>
    /// </remarks>
    public interface ITradeWorld
    {
        /// <summary>Whether the holder exists and can be traded with at all.</summary>
        bool Exists(Guid holder);

        /// <summary>Whether the holder is mid save, or otherwise must not be written to now.</summary>
        bool IsLocked(Guid holder);

        long CountOf(Guid holder, StackKey stack);

        long CoinsOf(Guid holder);

        /// <summary>Removes items. False if they were not all there, having changed nothing.</summary>
        bool TakeItems(Guid holder, StackKey stack, long amount);

        /// <summary>Adds items. False if they could not be delivered, having changed nothing.</summary>
        bool GiveItems(Guid holder, StackKey stack, long amount);

        bool TakeCoins(Guid holder, long amount);

        bool GiveCoins(Guid holder, long amount);
    }

    /// <summary>
    ///     Applies a <see cref="TradePlan"/>, all of it or none of it.
    /// </summary>
    /// <remarks>
    ///     The authoritative state is the in memory entity graph and the database is a batched
    ///     projection of it, so a trade does not need a distributed transaction. It needs two much
    ///     simpler things.
    ///
    ///     <para>
    ///     Everything is validated before anything is written. Most failures never become partial
    ///     states because they are caught before the first change, which is the half that does the
    ///     real work.
    ///     </para>
    ///
    ///     <para>
    ///     Anything applied is undone if a later step fails anyway. Validation cannot be perfect:
    ///     an add can be refused for reasons no check can see in advance, and between validating
    ///     and applying, another request may have moved the same stack. Every mutation here is a
    ///     field write on an object we hold a reference to, so its inverse is exact. Apply in
    ///     order, remember what was applied, and on failure walk it backwards.
    ///     </para>
    ///
    ///     <para>
    ///     What this does not cover is a crash between two writes. That needs the pending ledger
    ///     record described in the design document: write the transaction row as pending, apply,
    ///     mark it applied, and reconcile stragglers at startup. Nothing here needs to change for
    ///     that; it goes around this.
    ///     </para>
    /// </remarks>
    public sealed class TradeExecutor
    {
        private readonly ITradeWorld world;

        public TradeExecutor(ITradeWorld world)
        {
            this.world = world;
        }

        /// <summary>
        ///     Whether the plan could be applied against the world as it is now, and why not.
        /// </summary>
        /// <remarks>
        ///     Quantities are accumulated across the whole plan rather than checked one move at a
        ///     time, because two moves out of the same stack can each look affordable on their own
        ///     and be impossible together. That is the shape of a multi seller purchase, which is
        ///     exactly where this has to be right.
        /// </remarks>
        public TradeResult Validate(TradePlan plan)
        {
            if (plan == null) return TradeResult.Failed("There is nothing to do.");
            if (plan.IsEmpty) return TradeResult.Failed("There is nothing to do.");

            foreach (var holder in plan.Holders)
            {
                if (!world.Exists(holder))
                    return TradeResult.Failed("Someone in this trade no longer exists.");

                if (world.IsLocked(holder))
                    return TradeResult.Failed("That character is busy being saved. Try again in a moment.");
            }

            var itemsWanted = new Dictionary<(Guid, StackKey), long>();
            var coinsWanted = new Dictionary<Guid, long>();

            foreach (var move in plan.Items)
            {
                if (move.Amount <= 0)
                    return TradeResult.Failed("Nothing can move an amount of zero or less.");

                if (move.From == move.To)
                    return TradeResult.Failed("Items cannot move to where they already are.");

                if (move.From == ItemMove.Outside) continue;

                var key = (move.From, move.Stack);
                itemsWanted.TryGetValue(key, out var running);
                itemsWanted[key] = running + move.Amount;
            }

            foreach (var move in plan.Coins)
            {
                if (move.Amount <= 0)
                    return TradeResult.Failed("Nothing can move an amount of zero or less.");

                if (move.From == move.To)
                    return TradeResult.Failed("Coins cannot move to where they already are.");

                if (move.From == ItemMove.Outside) continue;

                coinsWanted.TryGetValue(move.From, out var running);
                coinsWanted[move.From] = running + move.Amount;
            }

            foreach (var wanted in itemsWanted)
            {
                var (holder, stack) = wanted.Key;
                if (world.CountOf(holder, stack) < wanted.Value)
                    return TradeResult.Failed("Some of those items are no longer there.");
            }

            foreach (var wanted in coinsWanted)
            {
                if (world.CoinsOf(wanted.Key) < wanted.Value)
                    return TradeResult.Failed("Not enough coins.");
            }

            return TradeResult.Success;
        }

        /// <summary>
        ///     Validates, then applies everything or leaves the world exactly as it was.
        /// </summary>
        public TradeResult Commit(TradePlan plan)
        {
            var check = Validate(plan);
            if (!check.Ok) return check;

            // What has actually been done, newest last, so failure can walk it backwards.
            var applied = new List<Action>();

            foreach (var move in plan.Items)
            {
                if (move.From != ItemMove.Outside)
                {
                    if (!world.TakeItems(move.From, move.Stack, move.Amount))
                        return Undo(applied, "Some of those items are no longer there.");

                    var from = move.From;
                    var stack = move.Stack;
                    var amount = move.Amount;
                    applied.Add(() => world.GiveItems(from, stack, amount));
                }

                if (move.To != ItemMove.Outside)
                {
                    if (!world.GiveItems(move.To, move.Stack, move.Amount))
                        return Undo(applied, "Those items could not be delivered.");

                    var to = move.To;
                    var stack = move.Stack;
                    var amount = move.Amount;
                    applied.Add(() => world.TakeItems(to, stack, amount));
                }
            }

            foreach (var move in plan.Coins)
            {
                if (move.From != ItemMove.Outside)
                {
                    if (!world.TakeCoins(move.From, move.Amount))
                        return Undo(applied, "Not enough coins.");

                    var from = move.From;
                    var amount = move.Amount;
                    applied.Add(() => world.GiveCoins(from, amount));
                }

                if (move.To != ItemMove.Outside)
                {
                    if (!world.GiveCoins(move.To, move.Amount))
                        return Undo(applied, "The coins could not be delivered.");

                    var to = move.To;
                    var amount = move.Amount;
                    applied.Add(() => world.TakeCoins(to, amount));
                }
            }

            return TradeResult.Success;
        }

        /// <summary>
        ///     Walks the applied steps backwards. Newest first, because an earlier step may be what
        ///     made a later one possible.
        /// </summary>
        /// <remarks>
        ///     Each inverse is the exact opposite of a write we know succeeded a moment ago on an
        ///     object we still hold, so there is no reason for one to fail. If one somehow does, the
        ///     rest still run: stopping would strand more than continuing does.
        /// </remarks>
        private TradeResult Undo(List<Action> applied, string reason)
        {
            for (var i = applied.Count - 1; i >= 0; i--)
            {
                try
                {
                    applied[i]();
                }
                catch
                {
                    // Nothing useful to do here that is not worse. The remaining steps still run,
                    // and the caller is told the trade did not happen either way.
                }
            }

            return TradeResult.Failed(reason);
        }
    }
}

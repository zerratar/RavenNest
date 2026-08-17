using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Game.Trading;
using RavenNest.DataModels;

namespace RavenNest.BusinessLogic.Game.ClanBank
{
    /// <summary>
    ///     Putting things into a clan's bank and taking them out again.
    /// </summary>
    /// <remarks>
    ///     Every move goes through <see cref="TradeExecutor"/>, so it either happens completely or
    ///     not at all. That matters more here than almost anywhere else on the site: the halfway
    ///     state of a bank move is items belonging to nobody, and nobody can prove it happened.
    ///
    ///     <para>
    ///     Which is also why every move is logged. A shared bank without a log is a grief vector
    ///     rather than a feature: the first time somebody empties it the clan needs to know who,
    ///     and without a record the honest answer is that nobody can tell.
    ///     </para>
    /// </remarks>
    public class ClanBankManager
    {
        private readonly GameData gameData;
        private readonly IClanManager clanManager;
        private readonly PlayerInventoryProvider inventoryProvider;
        private readonly ILogger<ClanBankManager> logger;

        public ClanBankManager(
            GameData gameData,
            IClanManager clanManager,
            PlayerInventoryProvider inventoryProvider,
            ILogger<ClanBankManager> logger)
        {
            this.gameData = gameData;
            this.clanManager = clanManager;
            this.inventoryProvider = inventoryProvider;
            this.logger = logger;
        }

        /// <summary>
        ///     Moves items from a character into the clan's bank.
        /// </summary>
        public TradeResult Deposit(Guid characterId, Guid clanId, StackKey stack, long amount)
        {
            var check = Check(characterId, clanId, amount, out var inventory);
            if (!check.Ok) return check;

            // Soulbound items are bound to the person holding them. Putting one in a shared bank is
            // how it stops being bound to anybody, which is the whole thing soulbound exists to
            // prevent, so it is refused rather than stripped.
            foreach (var item in inventory.GetUnequippedItems().Where(x => x.Key() == stack))
            {
                if (item.Soulbound)
                {
                    return TradeResult.Failed("Soulbound items cannot go into a clan bank.");
                }

                if (inventory.IsLocked(item.Id))
                {
                    return TradeResult.Failed("That item is busy being saved. Try again in a moment.");
                }
            }

            var plan = new TradePlan(
                TradeKind.ClanBankDeposit,
                new[] { new ItemMove(characterId, clanId, stack, amount) });

            var result = Run(characterId, clanId, inventory, plan);
            if (result.Ok) Record(clanId, characterId, stack.ItemId, amount);

            return result;
        }

        /// <summary>
        ///     Moves items out of the clan's bank to a character, if their rank allows it today.
        /// </summary>
        public TradeResult Withdraw(Guid characterId, Guid clanId, StackKey stack, long amount)
        {
            var check = Check(characterId, clanId, amount, out var inventory);
            if (!check.Ok) return check;

            var allowance = RemainingToday(characterId, clanId);
            if (allowance == 0)
            {
                return TradeResult.Failed("Your rank cannot take anything out of the clan bank.");
            }

            if (allowance > 0 && amount > allowance)
            {
                return TradeResult.Failed("That is more than your rank can take out today. " +
                                          allowance + " left.");
            }

            var plan = new TradePlan(
                TradeKind.ClanBankWithdraw,
                new[] { new ItemMove(clanId, characterId, stack, amount) });

            var result = Run(characterId, clanId, inventory, plan);
            if (result.Ok) Record(clanId, characterId, stack.ItemId, -amount);

            return result;
        }

        /// <summary>
        ///     How many more items this character may take out today. -1 means no limit.
        /// </summary>
        /// <remarks>
        ///     Summed from the log rather than counted separately, so there is one record of what
        ///     happened and the allowance cannot disagree with it.
        /// </remarks>
        public long RemainingToday(Guid characterId, Guid clanId)
        {
            var clan = gameData.GetClan(clanId);
            var character = gameData.GetCharacter(characterId);
            if (clan == null || character == null) return 0;

            // The owner is not a rank and is not limited by one.
            if (clan.UserId == character.UserId) return ClanBankDefaults.Unlimited;

            var membership = gameData.GetClanMembership(characterId);
            if (membership == null || membership.ClanId != clanId) return 0;

            var role = gameData.GetClanRole(membership.ClanRoleId);
            if (role == null) return 0;

            var limit = gameData.GetClanBankLimit(clanId, role.Level);
            var perDay = limit?.ItemsPerDay ?? ClanBankDefaults.ForRoleLevel(role.Level);

            if (perDay < 0) return ClanBankDefaults.Unlimited;
            if (perDay == 0) return 0;

            var taken = gameData.GetClanBankWithdrawnSince(characterId, DateTime.UtcNow.Date);
            return Math.Max(0, perDay - taken);
        }

        public IReadOnlyList<ClanBankItem> GetItems(Guid clanId) => gameData.GetClanBankItems(clanId);

        /// <summary>The log, newest first.</summary>
        public IReadOnlyList<ClanBankLog> GetLog(Guid clanId, int take = 200) =>
            gameData.GetClanBankLog(clanId)
                .OrderByDescending(x => x.Time)
                .Take(take)
                .ToList();

        /// <summary>
        ///     The checks both directions share: a real character, in this clan, allowed to use the
        ///     bank at all.
        /// </summary>
        private TradeResult Check(Guid characterId, Guid clanId, long amount, out PlayerInventory inventory)
        {
            inventory = null;

            if (amount <= 0) return TradeResult.Failed("Pick an amount above zero.");

            var character = gameData.GetCharacter(characterId);
            if (character == null) return TradeResult.Failed("That character no longer exists.");

            var clan = gameData.GetClan(clanId);
            if (clan == null) return TradeResult.Failed("That clan no longer exists.");

            // The owner may always use their own bank, whatever rank they hold in it.
            var isOwner = clan.UserId == character.UserId;

            if (!isOwner)
            {
                var membership = gameData.GetClanMembership(characterId);
                if (membership == null || membership.ClanId != clanId)
                {
                    return TradeResult.Failed("That character is not in this clan.");
                }

                var permissions = clanManager.GetClanRolePermissionsByCharacterId(characterId);
                if (permissions == null || !permissions.CanUseClanBank)
                {
                    return TradeResult.Failed("Your rank cannot use the clan bank.");
                }
            }

            inventory = inventoryProvider.Get(characterId);
            return TradeResult.Success;
        }

        private TradeResult Run(Guid characterId, Guid clanId, PlayerInventory inventory, TradePlan plan)
        {
            var world = new ClanBankWorld(gameData, inventory, characterId, clanId);
            var result = new TradeExecutor(world).Commit(plan);

            if (!result.Ok)
            {
                logger.LogWarning("Clan bank move refused for character '" + characterId +
                                  "' in clan '" + clanId + "': " + result.Reason);
            }

            return result;
        }

        /// <summary>
        ///     Writes what happened. Positive is a deposit, negative a withdrawal.
        /// </summary>
        /// <remarks>
        ///     After the move rather than before it, so the log never claims something that did not
        ///     happen. The gap this leaves is a crash between the two, which would lose the record
        ///     of a move that did happen; that is the pending ledger problem described in the trade
        ///     design document and is solved for every kind of move at once or not at all.
        /// </remarks>
        private void Record(Guid clanId, Guid characterId, Guid itemId, long amount)
        {
            gameData.Add(new ClanBankLog
            {
                Id = Guid.NewGuid(),
                ClanId = clanId,
                CharacterId = characterId,
                ItemId = itemId,
                Amount = amount,
                Time = DateTime.UtcNow
            });
        }
    }
}

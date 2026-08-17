using System;
using System.Linq;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Game.Trading;
using RavenNest.DataModels;

namespace RavenNest.BusinessLogic.Game.ClanBank
{
    /// <summary>
    ///     The two holders a clan bank move touches: one character, and one clan's bank.
    /// </summary>
    /// <remarks>
    ///     Deliberately built for one operation with exactly two holders named up front, rather than
    ///     a general world that works out what a holder id refers to. Character ids and clan ids are
    ///     both Guids, so a general one would have to guess, and guessing wrong would move somebody
    ///     else's items. Here there is nothing to guess: anything that is not the character is the
    ///     clan, and anything that is neither does not exist.
    ///
    ///     <para>
    ///     Coins are not part of a bank move and both coin methods refuse. A plan that tried to move
    ///     coins through here would be a plan built wrong, and failing is a better way to find that
    ///     out than quietly moving nothing.
    ///     </para>
    /// </remarks>
    public sealed class ClanBankWorld : ITradeWorld
    {
        private readonly GameData gameData;
        private readonly PlayerInventory inventory;
        private readonly Guid characterId;
        private readonly Guid clanId;

        public ClanBankWorld(GameData gameData, PlayerInventory inventory, Guid characterId, Guid clanId)
        {
            this.gameData = gameData;
            this.inventory = inventory;
            this.characterId = characterId;
            this.clanId = clanId;
        }

        public bool Exists(Guid holder)
        {
            if (holder == characterId) return gameData.GetCharacter(holder) != null;
            if (holder == clanId) return gameData.GetClan(holder) != null;
            return false;
        }

        /// <summary>
        ///     Nothing here takes a whole holder out of use.
        /// </summary>
        /// <remarks>
        ///     The inventory's lock is per item rather than per inventory, so it cannot be answered
        ///     at this level without lying in one direction or the other. The manager checks the
        ///     specific stack before building a plan, where it can say which item is busy.
        /// </remarks>
        public bool IsLocked(Guid holder) => false;

        public long CountOf(Guid holder, StackKey stack)
        {
            if (holder == clanId)
            {
                return gameData.GetClanBankStack(clanId, stack)?.Amount ?? 0;
            }

            if (holder != characterId) return 0;

            // Equipped items are not on offer. Taking the sword out of somebody's hand because it
            // matched a stack key is not what depositing means.
            return inventory.GetUnequippedItems()
                .Where(x => x.Key() == stack)
                .Sum(x => x.Amount);
        }

        public long CoinsOf(Guid holder) => 0;

        public bool TakeItems(Guid holder, StackKey stack, long amount)
        {
            if (holder == clanId) return TakeFromBank(stack, amount);
            if (holder != characterId) return false;

            // Across stacks, because one item can sit in several rows. Oldest first is arbitrary
            // and does not matter: they are the same item by definition of matching the key.
            var remaining = amount;

            foreach (var item in inventory.GetUnequippedItems().Where(x => x.Key() == stack).ToList())
            {
                if (remaining <= 0) break;

                var take = Math.Min(remaining, item.Amount);
                if (!inventory.RemoveItem(item, take)) return false;

                remaining -= take;
            }

            return remaining == 0;
        }

        public bool GiveItems(Guid holder, StackKey stack, long amount)
        {
            if (holder == clanId) return GiveToBank(stack, amount);
            if (holder != characterId) return false;

            return inventory.TryAddItem(
                stack.ItemId,
                amount,
                out _,
                equipped: false,
                tag: stack.Tag,
                soulbound: false,
                enchantment: stack.Enchantment,
                name: stack.Name,
                transmogrificationId: stack.TransmogrificationId,
                flags: stack.Flags);
        }

        public bool TakeCoins(Guid holder, long amount) => false;

        public bool GiveCoins(Guid holder, long amount) => false;

        private bool TakeFromBank(StackKey stack, long amount)
        {
            var row = gameData.GetClanBankStack(clanId, stack);
            if (row == null || row.Amount < amount) return false;

            row.Amount -= amount;

            // An empty row is not a holding of nothing, it is noise in every list that reads the
            // bank. The stack is recreated by the next deposit.
            if (row.Amount == 0)
            {
                gameData.Remove(row);
            }

            return true;
        }

        private bool GiveToBank(StackKey stack, long amount)
        {
            var row = gameData.GetClanBankStack(clanId, stack);
            if (row != null)
            {
                row.Amount += amount;
                return true;
            }

            gameData.Add(new ClanBankItem
            {
                Id = Guid.NewGuid(),
                ClanId = clanId,
                ItemId = stack.ItemId,
                Amount = amount,
                Tag = stack.Tag,
                Enchantment = stack.Enchantment,
                Name = stack.Name,
                TransmogrificationId = stack.TransmogrificationId,
                Flags = stack.Flags,

                // Soulbound items never reach here: the manager refuses them at deposit. Written
                // as false rather than copied, so a bug upstream cannot mark clan property as
                // bound to somebody.
                Soulbound = false
            });

            return true;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Game.Trading;
using RavenNest.DataModels;

namespace RavenNest.BusinessLogic.Game.StreamerShop
{
    /// <summary>
    ///     The holders a tavern shelf move touches, each named up front: the streamer's stash, a
    ///     character (the streamer's own when listing or returning, the buyer's when buying), and the
    ///     village whose treasury the coins go to.
    /// </summary>
    /// <remarks>
    ///     The listing itself is <see cref="ItemMove.Outside"/>: items go out to it when listed and come
    ///     back from it when bought or returned, and the listing row is written or changed by the manager
    ///     once the move has happened. Like <see cref="ClanBank.ClanBankWorld"/>, nothing is guessed:
    ///     a holder that was not added does not exist.
    ///
    ///     <para>
    ///     Coins are per account in this game, so a character's coins are its user's. The village's are
    ///     the village's own resources row.
    ///     </para>
    ///
    ///     <para>
    ///     What it took from and added to which inventory rows is kept, so the manager can tell the game
    ///     exactly which rows changed once the trade has gone through.
    ///     </para>
    /// </remarks>
    public sealed class StreamerShopWorld : ITradeWorld
    {
        private readonly GameData gameData;
        private readonly PlayerInventoryProvider inventories;

        private Guid? stashUserId;
        private readonly Dictionary<Guid, PlayerInventory> characters = new Dictionary<Guid, PlayerInventory>();
        private Guid? villageId;
        private Resources villageResources;

        public StreamerShopWorld(GameData gameData, PlayerInventoryProvider inventories)
        {
            this.gameData = gameData;
            this.inventories = inventories;
        }

        /// <summary>Inventory rows items were taken from: character, row, item, amount.</summary>
        public List<(Guid CharacterId, Guid InventoryItemId, Guid ItemId, long Amount)> Taken { get; } = new List<(Guid, Guid, Guid, long)>();

        /// <summary>Inventory rows items were added to: character, the row, how many.</summary>
        public List<(Guid CharacterId, InventoryItem Row, long Amount)> Added { get; } = new List<(Guid, InventoryItem, long)>();

        public StreamerShopWorld WithStash(Guid userId)
        {
            stashUserId = userId;
            return this;
        }

        public StreamerShopWorld WithCharacter(Guid characterId)
        {
            characters[characterId] = inventories.Get(characterId);
            return this;
        }

        public StreamerShopWorld WithVillage(Village village)
        {
            villageId = village.Id;
            villageResources = gameData.GetResources(village.ResourcesId);
            return this;
        }

        public bool Exists(Guid holder)
        {
            if (stashUserId == holder) return gameData.GetUser(holder) != null;
            if (characters.ContainsKey(holder)) return gameData.GetCharacter(holder) != null;
            if (villageId == holder) return villageResources != null;
            return false;
        }

        /// <summary>The inventory's locks are per item; the manager checks the stack it is about to list.</summary>
        public bool IsLocked(Guid holder) => false;

        public long CountOf(Guid holder, StackKey stack)
        {
            if (stashUserId == holder)
            {
                return gameData.GetUserBankItems(holder).Where(x => x.Key() == stack).Sum(x => x.Amount);
            }

            if (characters.TryGetValue(holder, out var inventory))
            {
                // equipped items are never on offer
                return inventory.GetUnequippedItems().Where(x => x.Key() == stack).Sum(x => x.Amount);
            }

            return 0;
        }

        public long CoinsOf(Guid holder)
        {
            var resources = CoinsHolder(holder);
            return resources != null ? (long)Math.Floor(resources.Coins) : 0;
        }

        public bool TakeItems(Guid holder, StackKey stack, long amount)
        {
            if (stashUserId == holder) return TakeFromStash(holder, stack, amount);
            if (!characters.TryGetValue(holder, out var inventory)) return false;

            var remaining = amount;
            foreach (var item in inventory.GetUnequippedItems().Where(x => x.Key() == stack).ToList())
            {
                if (remaining <= 0) break;
                var take = Math.Min(remaining, item.Amount);
                if (!inventory.RemoveItem(item, take)) return false;
                Taken.Add((holder, item.Id, item.ItemId, take));
                remaining -= take;
            }

            return remaining == 0;
        }

        public bool GiveItems(Guid holder, StackKey stack, long amount)
        {
            if (stashUserId == holder) return GiveToStash(holder, stack, amount);
            if (!characters.TryGetValue(holder, out var inventory)) return false;

            if (!inventory.TryAddItem(
                stack.ItemId,
                amount,
                out var added,
                equipped: false,
                tag: stack.Tag,
                soulbound: false,
                enchantment: stack.Enchantment,
                name: stack.Name,
                transmogrificationId: stack.TransmogrificationId,
                flags: stack.Flags))
            {
                return false;
            }

            // one row took them all (a stack), or one row each (items that do not stack)
            foreach (var row in added)
            {
                Added.Add((holder, row, added.Count == 1 ? amount : 1));
            }

            return true;
        }

        public bool TakeCoins(Guid holder, long amount)
        {
            var resources = CoinsHolder(holder);
            if (resources == null || resources.Coins < amount) return false;
            resources.Coins -= amount;
            return true;
        }

        public bool GiveCoins(Guid holder, long amount)
        {
            var resources = CoinsHolder(holder);
            if (resources == null) return false;
            resources.Coins += amount;
            return true;
        }

        private Resources CoinsHolder(Guid holder)
        {
            if (villageId == holder) return villageResources;
            if (characters.ContainsKey(holder))
            {
                var character = gameData.GetCharacter(holder);
                return character != null ? gameData.GetResources(character) : null;
            }

            return null;
        }

        private bool TakeFromStash(Guid userId, StackKey stack, long amount)
        {
            var remaining = amount;
            foreach (var row in gameData.GetUserBankItems(userId).Where(x => x.Key() == stack).ToList())
            {
                if (remaining <= 0) break;
                var take = Math.Min(remaining, row.Amount);
                row.Amount -= take;
                if (row.Amount == 0) gameData.Remove(row);
                remaining -= take;
            }

            return remaining == 0;
        }

        private bool GiveToStash(Guid userId, StackKey stack, long amount)
        {
            var row = gameData.GetUserBankItems(userId).FirstOrDefault(x => x.Key() == stack);
            if (row != null)
            {
                row.Amount += amount;
                return true;
            }

            gameData.Add(new UserBankItem
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ItemId = stack.ItemId,
                Amount = amount,
                Tag = stack.Tag,
                Enchantment = stack.Enchantment,
                Name = stack.Name,
                TransmogrificationId = stack.TransmogrificationId,
                Flags = stack.Flags,
                // soulbound items are never listed, so nothing coming back from a shelf is bound
                Soulbound = false
            });

            return true;
        }
    }
}

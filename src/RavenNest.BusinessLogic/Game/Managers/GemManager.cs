using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Extensions;
using RavenNest.Combat;
using RavenNest.DataModels;
using RavenNest.Models;
using Character = RavenNest.DataModels.Character;
using ItemCategory = RavenNest.DataModels.ItemCategory;
using ItemType = RavenNest.DataModels.ItemType;

namespace RavenNest.BusinessLogic.Game
{
    /// <summary>
    ///     Gems and sockets (docs/gems-design.md in the game repo): a clan's Enchanting turns raw gems into enchanted
    ///     ones, three of a tier combine into the next for coins, and gems go into gear's sockets and come out again
    ///     (destroyed, unless the keep roll succeeds). The rules are RavenNest.Combat.Gems; this applies them to
    ///     inventories. Changes reach the game through the inventory feed.
    /// </summary>
    public class GemManager
    {
        /// <summary>The most gems made or combined by one command.</summary>
        public const int MaxPerCommand = 100;

        private readonly GameData gameData;
        private readonly PlayerInventoryProvider inventories;
        private readonly ILogger<GemManager> logger;
        private readonly Random random = new Random();

        // one socket change at a time: the game (!socket) and the website reach the same items, and two changes
        // working from the same reading of an item's sockets would lose a gem
        private readonly object socketMutex = new object();

        public GemManager(GameData gameData, PlayerInventoryProvider inventories, ILogger<GemManager> logger)
        {
            this.gameData = gameData;
            this.inventories = inventories;
            this.logger = logger;
        }

        /// <summary>Enchants up to <paramref name="amount"/> raw gems of a kind with the clan's Enchanting; the tier is rolled per gem.</summary>
        public GemResult Enchant(SessionToken token, Guid characterId, int kind, int amount)
        {
            if (!Valid(kind)) return GemResult.Fail(GemResultStatus.NoRawGem);
            var character = GetCharacter(token, characterId);
            if (character == null) return GemResult.Fail(GemResultStatus.Error);

            var membership = gameData.GetClanMembership(character.Id);
            if (membership == null) return GemResult.Fail(GemResultStatus.NotInClan);
            var level = ClanEnchantingLevel(character);
            if (level <= 0) return GemResult.Fail(GemResultStatus.NoEnchanting);

            var gemKind = (GemKind)kind;
            var raw = FindItem(Gems.RawName(gemKind));
            if (raw == null) return GemResult.Fail(GemResultStatus.NoRawGem);

            var inventory = inventories.Get(character.Id);
            var stacks = inventory.GetAllItems().Where(x => x.ItemId == raw.Id && !x.Equipped && x.Amount > 0).ToList();
            var have = stacks.Sum(x => x.Amount);
            if (have <= 0) return new GemResult { Status = GemResultStatus.NoRawGem, Kind = kind };

            var todo = (int)Math.Min(have, Math.Clamp(amount, 1, MaxPerCommand));
            var counts = new int[Gems.TierCount];
            var used = 0;
            foreach (var stack in stacks)
            {
                while (used < todo && TakeOne(inventory, stack.Id))
                {
                    var tier = Gems.RollTier(level, random.NextDouble());
                    counts[(int)tier]++;
                    used++;
                }
                if (used >= todo) break;
            }

            if (used == 0) return GemResult.Fail(GemResultStatus.Error);
            for (var t = 0; t < counts.Length; ++t)
            {
                if (counts[t] > 0) AddGem(inventory, gemKind, (GemTier)t, counts[t]);
            }

            return new GemResult { Status = GemResultStatus.Ok, Kind = kind, Counts = counts };
        }

        /// <summary>Combines three gems of a kind and tier into one of the next tier, up to <paramref name="times"/> times, for coins each time.</summary>
        public GemResult Combine(SessionToken token, Guid characterId, int kind, int tier, int times)
        {
            if (!Valid(kind) || tier < 0 || tier >= Gems.TierCount) return GemResult.Fail(GemResultStatus.NoGem);
            if (tier == Gems.TierCount - 1) return new GemResult { Status = GemResultStatus.HighestTier, Kind = kind, Tier = tier };
            var character = GetCharacter(token, characterId);
            if (character == null) return GemResult.Fail(GemResultStatus.Error);
            var resources = gameData.GetResources(character);
            if (resources == null) return GemResult.Fail(GemResultStatus.Error);

            var inventory = inventories.Get(character.Id);
            var gemId = Gems.ItemId((GemKind)kind, (GemTier)tier);
            var have = inventory.GetAllItems().Where(x => x.ItemId == gemId && !x.Equipped).Sum(x => x.Amount);
            // Gem Cutter (the Artisan skill tree) takes up to 30% off the fee
            var fee = RavenNest.Combat.TradeSkills.CombineFee(Gems.CombineFee[tier], SkillTreeManager.BuildOf(gameData, character.Id));
            var possible = (int)Math.Min(have / 3, Math.Clamp(times, 1, MaxPerCommand));
            if (possible <= 0) return new GemResult { Status = GemResultStatus.NotEnoughGems, Kind = kind, Tier = tier };
            possible = (int)Math.Min(possible, (long)(resources.Coins / fee));
            if (possible <= 0) return new GemResult { Status = GemResultStatus.NotEnoughCoins, Kind = kind, Tier = tier, Coins = fee };

            var made = 0;
            for (var i = 0; i < possible; ++i)
            {
                if (!TakeGems(inventory, gemId, 3)) break;
                resources.Coins -= fee;
                made++;
            }
            if (made == 0) return GemResult.Fail(GemResultStatus.Error);
            AddGem(inventory, (GemKind)kind, (GemTier)(tier + 1), made);
            return new GemResult { Status = GemResultStatus.Ok, Kind = kind, Tier = tier + 1, Counts = new[] { made }, Coins = fee * made };
        }

        /// <summary>
        ///     Puts a gem of a kind (the tier named, or the best one owned) into an item: the first free socket, or the
        ///     one named (1 based), which destroys the gem there unless the keep roll succeeds.
        /// </summary>
        public GemResult Socket(SessionToken token, Guid characterId, Guid inventoryItemId, int kind, int tier, int socket, bool confirm = false)
        {
            return Socket(GetCharacter(token, characterId), inventoryItemId, kind, tier, socket, confirm, null, out _);
        }

        /// <summary>
        ///     The same from the website, for a character of this account, in a game or not (a running game hears of it
        ///     through the inventory feed, like any inventory change). Socketing over a gem answers NeedsConfirm until
        ///     <paramref name="confirmedGem"/> names the gem in that socket: the one the player was shown and agreed to
        ///     risk. <paramref name="socketedItemId"/> is the stack that holds the gems afterwards (one of a stack of
        ///     more splits off).
        /// </summary>
        public GemResult SocketForOwner(Guid userId, Guid characterId, Guid inventoryItemId, int kind, int tier, int socket, Guid? confirmedGem, out Guid socketedItemId)
        {
            return Socket(GetOwnedCharacter(userId, characterId), inventoryItemId, kind, tier, socket, confirmedGem.HasValue, confirmedGem, out socketedItemId);
        }

        // Both paths end here, with the character each has checked to be the caller's (null when it was not).
        private GemResult Socket(Character character, Guid inventoryItemId, int kind, int tier, int socket, bool confirm, Guid? confirmedGem, out Guid socketedItemId)
        {
            socketedItemId = inventoryItemId;
            if (!ValidAny(kind)) return GemResult.Fail(GemResultStatus.NoGem);
            if (character == null) return GemResult.Fail(GemResultStatus.Error);
            lock (socketMutex)
            {
                var inventory = inventories.Get(character.Id);
                var item = inventory.Get(inventoryItemId);
                if (item.IsNull() || item.Item == null) return GemResult.Fail(GemResultStatus.NoSuchItem);
                if (inventory.IsLocked(item.Id)) return GemResult.Fail(GemResultStatus.Busy);
                if (!TryGetSlot(item.Item, out var slot)) return new GemResult { Status = GemResultStatus.NotSocketable, ItemName = NameOf(item) };
                // a Shadowstone works in armor only (Substitution); the game and the website say so before they ask
                if (!Gems.FitsIn((GemKind)kind, slot)) return new GemResult { Status = GemResultStatus.NotSocketable, Kind = kind, ItemName = NameOf(item) };
                var count = SocketCount(item, slot);
                if (count == 0) return new GemResult { Status = GemResultStatus.NoSockets, ItemName = NameOf(item) };

                var sockets = Gems.ParseSockets(item.Sockets, count);
                var index = socket - 1;
                if (socket <= 0)
                {
                    index = Array.FindIndex(sockets, x => !x.HasValue);
                    if (index < 0) return new GemResult { Status = GemResultStatus.SocketsFull, ItemName = NameOf(item), SocketCount = count };
                }
                else if (index >= count) return new GemResult { Status = GemResultStatus.NoSuchSocket, ItemName = NameOf(item), SocketCount = count };

                // the gem: of the tier named, or the best one owned
                var gemKind = (GemKind)kind;
                var gemTier = -1;
                for (var t = Gems.TierCount - 1; t >= 0; --t)
                {
                    if (tier >= 0 && t != tier) continue;
                    var id = Gems.ItemId(gemKind, (GemTier)t);
                    if (inventory.GetAllItems().Any(x => x.ItemId == id && !x.Equipped && x.Amount > 0)) { gemTier = t; break; }
                }
                if (gemTier < 0) return new GemResult { Status = GemResultStatus.NoGem, Kind = kind, Tier = tier };

                // socketing over a gem destroys it unless the keep roll succeeds: said first, done when confirmed (a
                // Shadowstone always comes back, so there is nothing to confirm)
                if (sockets[index].HasValue && !Gems.IsShadowstone(sockets[index].Value) && !Confirmed(confirm, confirmedGem, sockets[index].Value)) return NeedsConfirm(character, item, sockets[index].Value, index, count);

                var result = new GemResult { Status = GemResultStatus.Ok, Kind = kind, Tier = gemTier, ItemName = NameOf(item), Socket = index + 1, SocketCount = count };
                var target = inventory.Get(item, true);
                try
                {
                    if (!TakeGems(inventory, Gems.ItemId(gemKind, (GemTier)gemTier), 1)) return GemResult.Fail(GemResultStatus.Error);

                    if (sockets[index].HasValue) Remove(character, inventory, sockets[index].Value, result);
                    sockets[index] = Gems.ItemId(gemKind, (GemTier)gemTier);
                    socketedItemId = SetSockets(inventory, target, Gems.FormatSockets(sockets))?.Id ?? inventoryItemId;
                    return result;
                }
                finally
                {
                    inventory.Unlock(item.Id);
                }
            }
        }

        /// <summary>Takes the gem out of a socket (1 based): it is destroyed unless the keep roll succeeds.</summary>
        public GemResult Unsocket(SessionToken token, Guid characterId, Guid inventoryItemId, int socket, bool confirm = false)
        {
            return Unsocket(GetCharacter(token, characterId), inventoryItemId, socket, confirm, null, out _);
        }

        /// <summary>
        ///     The same from the website, for a character of this account, in a game or not. Answers NeedsConfirm until
        ///     <paramref name="confirmedGem"/> names the gem in that socket: the one the player was shown and agreed to
        ///     risk. <paramref name="socketedItemId"/> is the stack the item is afterwards.
        /// </summary>
        public GemResult UnsocketForOwner(Guid userId, Guid characterId, Guid inventoryItemId, int socket, Guid? confirmedGem, out Guid socketedItemId)
        {
            return Unsocket(GetOwnedCharacter(userId, characterId), inventoryItemId, socket, confirmedGem.HasValue, confirmedGem, out socketedItemId);
        }

        // Both paths end here, with the character each has checked to be the caller's (null when it was not).
        private GemResult Unsocket(Character character, Guid inventoryItemId, int socket, bool confirm, Guid? confirmedGem, out Guid socketedItemId)
        {
            socketedItemId = inventoryItemId;
            if (character == null) return GemResult.Fail(GemResultStatus.Error);
            lock (socketMutex)
            {
                var inventory = inventories.Get(character.Id);
                var item = inventory.Get(inventoryItemId);
                if (item.IsNull() || item.Item == null) return GemResult.Fail(GemResultStatus.NoSuchItem);
                if (inventory.IsLocked(item.Id)) return GemResult.Fail(GemResultStatus.Busy);
                if (!TryGetSlot(item.Item, out var slot)) return new GemResult { Status = GemResultStatus.NotSocketable, ItemName = NameOf(item) };
                var count = SocketCount(item, slot);
                var sockets = Gems.ParseSockets(item.Sockets, count);
                var index = socket - 1;
                if (socket <= 0)
                {
                    index = Array.FindLastIndex(sockets, x => x.HasValue);
                    // no socket named and no gem in any: "has no gems in it" (Socket 0), not "has 2 socket(s)"
                    if (index < 0 && count > 0) return new GemResult { Status = GemResultStatus.EmptySocket, ItemName = NameOf(item), Socket = 0, SocketCount = count };
                }
                if (index < 0 || index >= count) return new GemResult { Status = count == 0 ? GemResultStatus.NoSockets : GemResultStatus.NoSuchSocket, ItemName = NameOf(item), SocketCount = count };
                if (!sockets[index].HasValue) return new GemResult { Status = GemResultStatus.EmptySocket, ItemName = NameOf(item), Socket = index + 1, SocketCount = count };
                if (!Gems.IsShadowstone(sockets[index].Value) && !Confirmed(confirm, confirmedGem, sockets[index].Value)) return NeedsConfirm(character, item, sockets[index].Value, index, count);

                var result = new GemResult { Status = GemResultStatus.Ok, ItemName = NameOf(item), Socket = index + 1, SocketCount = count };
                var target = inventory.Get(item, true);
                try
                {
                    Remove(character, inventory, sockets[index].Value, result);
                    sockets[index] = null;
                    socketedItemId = SetSockets(inventory, target, Gems.FormatSockets(sockets))?.Id ?? inventoryItemId;
                    return result;
                }
                finally
                {
                    inventory.Unlock(item.Id);
                }
            }
        }

        // The game confirms by asking again (its player was told the gem and the chance in chat). The website also names
        // the gem it showed, so a confirm click cannot destroy another gem that got into the socket in between.
        private static bool Confirmed(bool confirm, Guid? confirmedGem, Guid gemInSocket)
        {
            return confirm && (!confirmedGem.HasValue || confirmedGem.Value == gemInSocket);
        }

        // What taking this gem out would risk, with nothing done (docs/gems-design.md, section 4: the chance is said
        // before the player confirms; a Perfect gem replaced by a Chipped one was gone without a word).
        private GemResult NeedsConfirm(Character character, ReadOnlyInventoryItem item, Guid gemId, int index, int count)
        {
            var result = new GemResult { Status = GemResultStatus.NeedsConfirm, ItemName = NameOf(item), Socket = index + 1, SocketCount = count, KeepChance = KeepChance(character) };
            if (Gems.TryGetGem(gemId, out var oldKind, out var oldTier))
            {
                result.OldKind = (int)oldKind;
                result.OldTier = (int)oldTier;
            }
            return result;
        }

        /// <summary>The chance this character has to keep a gem taken out: the clan's Enchanting and the Gem Salvager node.</summary>
        public double KeepChance(Character character)
        {
            // a rank held counts whatever the levels: the node moved from Hunter to Artisan (2026-11-22), and a build
            // that learned it there keeps what it paid for
            var salvager = character != null ? SkillTreeManager.BuildOf(gameData, character.Id).Rank("gem-salvager") : 0;
            return Gems.KeepChance(ClanEnchantingLevel(character), salvager);
        }

        /// <summary>
        ///     A gem comes out of its socket: kept or crumbled by the keep roll. A Shadowstone always comes back: it is a
        ///     found-only chase item that cannot be made again, and a player who levels up must be able to move it into
        ///     their next armor; a roll that could destroy it would keep it stuck in old gear (docs/substitution-escape-idea.md).
        /// </summary>
        private void Remove(Character character, PlayerInventory inventory, Guid gemId, GemResult result)
        {
            if (!Gems.TryGetGem(gemId, out var kind, out var tier)) return;
            result.OldKind = (int)kind;
            result.OldTier = (int)tier;
            result.KeepChance = kind == GemKind.Shadowstone ? 1.0 : KeepChance(character);
            result.Kept = kind == GemKind.Shadowstone || random.NextDouble() < result.KeepChance;
            if (result.Kept) AddGem(inventory, kind, tier, 1);
        }

        // an item of more than one splits: one of them gets the gems, the rest stay a plain stack.
        // Returns the stack that holds the sockets now (null when nothing was set).
        /// <summary>An inventory item's sockets: by its level, or a Legendary's by its awakening rank (docs/legendary-design.md).</summary>
        private static int SocketCount(ReadOnlyInventoryItem item, GemSlot slot) =>
            Legendary.IsLegendary(item.ItemId) ? Legendary.Sockets(Legendary.RankOfTag(item.Tag)) : Gems.Sockets(GameMath.GetItemLevel(item.Item), slot);

        private DataModels.InventoryItem SetSockets(PlayerInventory inventory, DataModels.InventoryItem stack, string sockets)
        {
            if (stack == null) return null;
            if (stack.Amount.GetValueOrDefault(1) <= 1)
            {
                stack.Sockets = sockets;
                return stack;
            }

            if (!inventory.RemoveItem(stack, 1)) return null;
            var single = inventory.AddItemStack(stack, 1);
            single.Equipped = false;
            single.Sockets = sockets;
            return single;
        }

        private static bool TakeOne(PlayerInventory inventory, Guid stackId)
        {
            var stack = inventory.Get(stackId);
            if (stack.IsNull() || stack.Amount <= 0) return false;
            return inventory.RemoveItem(stack, 1);
        }

        private static bool TakeGems(PlayerInventory inventory, Guid gemId, int amount)
        {
            var stacks = inventory.GetAllItems().Where(x => x.ItemId == gemId && !x.Equipped && x.Amount > 0).ToList();
            if (stacks.Sum(x => x.Amount) < amount) return false;
            var left = amount;
            foreach (var s in stacks)
            {
                var take = (int)Math.Min(left, s.Amount);
                if (take > 0 && inventory.RemoveItem(s, take)) left -= take;
                if (left <= 0) return true;
            }
            return left <= 0;
        }

        private void AddGem(PlayerInventory inventory, GemKind kind, GemTier tier, int amount)
        {
            var id = Gems.ItemId(kind, tier);
            if (gameData.GetItem(id) == null)
            {
                logger.LogError("Gem item {Name} ({Id}) is missing; nothing added.", Gems.Name(kind, tier), id);
                return;
            }
            inventory.AddItem(id, amount);
        }

        /// <summary>Weapons, armour (shields too), rings and amulets take gems; tools, pets and looks do not.</summary>
        public static bool TryGetSlot(DataModels.Item item, out GemSlot slot)
        {
            slot = GemSlot.Armor;
            switch ((ItemCategory)item.Category)
            {
                case ItemCategory.Weapon: slot = GemSlot.Weapon; return true;
                case ItemCategory.Ring:
                case ItemCategory.Amulet: slot = GemSlot.Jewellery; return true;
                case ItemCategory.Armor:
                    var type = (ItemType)item.Type;
                    // hats, masks and hoods are looks and take no gems, except class gear's: a mage's hat is their helmet
                    return RavenNest.Models.ItemLines.IsClassGear((RavenNest.Models.ItemMaterial)item.Material)
                        || (type != ItemType.Hat && type != ItemType.Mask && type != ItemType.HeadCovering);
            }
            return false;
        }

        /// <summary>The same rule for the models the website reads.</summary>
        public static bool TryGetSlot(RavenNest.Models.Item item, out GemSlot slot)
        {
            slot = GemSlot.Armor;
            if (item == null) return false;
            switch (item.Category)
            {
                case RavenNest.Models.ItemCategory.Weapon: slot = GemSlot.Weapon; return true;
                case RavenNest.Models.ItemCategory.Ring:
                case RavenNest.Models.ItemCategory.Amulet: slot = GemSlot.Jewellery; return true;
                case RavenNest.Models.ItemCategory.Armor:
                    return RavenNest.Models.ItemLines.IsClassGear(item.Material)
                        || (item.Type != RavenNest.Models.ItemType.Hat && item.Type != RavenNest.Models.ItemType.Mask && item.Type != RavenNest.Models.ItemType.HeadCovering);
            }
            return false;
        }

        /// <summary>One socket of an item for showing: its number (1 based) and the gem in it, if any.</summary>
        public sealed class SocketView
        {
            public int Number { get; set; }
            /// <summary>The gem's item id; null for an empty socket.</summary>
            public Guid? GemId { get; set; }
            public GemKind Kind { get; set; }
            public GemTier Tier { get; set; }
            /// <summary>"Flawless Ruby"; null for an empty socket.</summary>
            public string Name { get; set; }
            /// <summary>What the gem gives in this kind of item: "+18% Fire Damage"; null for an empty socket.</summary>
            public string Bonus { get; set; }
            public bool Empty => !GemId.HasValue;
        }

        /// <summary>An item's sockets, first to last; none for an item that cannot have any (or not yet, by its level).</summary>
        public static System.Collections.Generic.List<SocketView> SocketsOf(RavenNest.Models.Item item, string sockets, string tag = null)
        {
            var views = new System.Collections.Generic.List<SocketView>();
            if (!TryGetSlot(item, out var slot)) return views;
            var count = Legendary.IsLegendary(item.Id) ? Legendary.Sockets(Legendary.RankOfTag(tag))
                : Gems.Sockets(Gems.ItemLevel(item.RequiredAttackLevel, item.RequiredDefenseLevel, item.RequiredMagicLevel, item.RequiredRangedLevel, item.RequiredSlayerLevel, item.Level), slot);
            var parsed = Gems.ParseSockets(sockets, count);
            for (var s = 0; s < parsed.Length; ++s)
            {
                var view = new SocketView { Number = s + 1 };
                if (parsed[s].HasValue && Gems.TryGetGem(parsed[s].Value, out var kind, out var tier))
                {
                    view.GemId = parsed[s].Value;
                    view.Kind = kind;
                    view.Tier = tier;
                    view.Name = Gems.Name(kind, tier);
                    view.Bonus = Gems.Describe(kind, tier, slot);
                }
                views.Add(view);
            }
            return views;
        }

        /// <summary>An item's sockets for showing: ("Socket 1", "Flawless Ruby: +18% Fire Damage") or ("Socket 2", "empty").</summary>
        public static System.Collections.Generic.List<(string Label, string Value)> SocketLines(RavenNest.Models.Item item, string sockets, string tag = null)
        {
            var lines = new System.Collections.Generic.List<(string, string)>();
            foreach (var socket in SocketsOf(item, sockets, tag))
            {
                lines.Add(("Socket " + socket.Number, socket.Empty ? "empty" : socket.Name + ": " + socket.Bonus));
            }
            return lines;
        }

        private int ClanEnchantingLevel(Character character)
        {
            var membership = gameData.GetClanMembership(character.Id);
            if (membership == null) return 0;
            var enchanting = gameData.GetSkills().FirstOrDefault(x => x.Name == "Enchanting");
            if (enchanting == null) return 0;
            var skill = gameData.GetClanSkills(membership.ClanId)?.FirstOrDefault(x => x.SkillId == enchanting.Id);
            return skill?.Level ?? 0;
        }

        private DataModels.Item FindItem(string name) => gameData.GetItems().FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));

        private static string NameOf(ReadOnlyInventoryItem item) => string.IsNullOrEmpty(item.Name) ? item.Item?.Name : item.Name;

        // the gems that are made (enchanted and combined): not the found-only Shadowstone
        private static bool Valid(int kind) => kind >= 0 && kind < Gems.KindCount && Gems.IsCraftable((GemKind)kind);

        // any gem, for sockets
        private static bool ValidAny(int kind) => kind >= 0 && kind < Gems.AllKindCount;

        private Character GetCharacter(SessionToken token, Guid characterId)
        {
            var session = gameData.GetSession(token.SessionId);
            if (session == null) return null;
            return gameData.GetActiveSessionCharacters(session)?.FirstOrDefault(x => x.Id == characterId);
        }

        // the website: a character of this account, in a game or not
        private Character GetOwnedCharacter(Guid userId, Guid characterId)
        {
            if (userId == Guid.Empty) return null;
            var character = gameData.GetCharacter(characterId);
            return character != null && character.UserId == userId ? character : null;
        }
    }
}

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
            var fee = Gems.CombineFee[tier];
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
        public GemResult Socket(SessionToken token, Guid characterId, Guid inventoryItemId, int kind, int tier, int socket)
        {
            if (!Valid(kind)) return GemResult.Fail(GemResultStatus.NoGem);
            var character = GetCharacter(token, characterId);
            if (character == null) return GemResult.Fail(GemResultStatus.Error);
            var inventory = inventories.Get(character.Id);
            var item = inventory.Get(inventoryItemId);
            if (item.IsNull() || item.Item == null) return GemResult.Fail(GemResultStatus.NoSuchItem);
            if (inventory.IsLocked(item.Id)) return GemResult.Fail(GemResultStatus.Busy);
            if (!TryGetSlot(item.Item, out var slot)) return new GemResult { Status = GemResultStatus.NotSocketable, ItemName = NameOf(item) };
            var count = Gems.Sockets(GameMath.GetItemLevel(item.Item), slot);
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
            if (gemTier < 0) return new GemResult { Status = GemResultStatus.NoGem, Kind = kind, Tier = Math.Max(0, tier) };

            var result = new GemResult { Status = GemResultStatus.Ok, Kind = kind, Tier = gemTier, ItemName = NameOf(item), Socket = index + 1, SocketCount = count };
            var target = inventory.Get(item, true);
            try
            {
                if (!TakeGems(inventory, Gems.ItemId(gemKind, (GemTier)gemTier), 1)) return GemResult.Fail(GemResultStatus.Error);

                if (sockets[index].HasValue) Remove(character, inventory, sockets[index].Value, result);
                sockets[index] = Gems.ItemId(gemKind, (GemTier)gemTier);
                SetSockets(inventory, target, Gems.FormatSockets(sockets));
                return result;
            }
            finally
            {
                inventory.Unlock(item.Id);
            }
        }

        /// <summary>Takes the gem out of a socket (1 based): it is destroyed unless the keep roll succeeds.</summary>
        public GemResult Unsocket(SessionToken token, Guid characterId, Guid inventoryItemId, int socket)
        {
            var character = GetCharacter(token, characterId);
            if (character == null) return GemResult.Fail(GemResultStatus.Error);
            var inventory = inventories.Get(character.Id);
            var item = inventory.Get(inventoryItemId);
            if (item.IsNull() || item.Item == null) return GemResult.Fail(GemResultStatus.NoSuchItem);
            if (inventory.IsLocked(item.Id)) return GemResult.Fail(GemResultStatus.Busy);
            if (!TryGetSlot(item.Item, out var slot)) return new GemResult { Status = GemResultStatus.NotSocketable, ItemName = NameOf(item) };
            var count = Gems.Sockets(GameMath.GetItemLevel(item.Item), slot);
            var sockets = Gems.ParseSockets(item.Sockets, count);
            var index = socket - 1;
            if (socket <= 0) index = Array.FindLastIndex(sockets, x => x.HasValue);
            if (index < 0 || index >= count) return new GemResult { Status = count == 0 ? GemResultStatus.NoSockets : GemResultStatus.NoSuchSocket, ItemName = NameOf(item), SocketCount = count };
            if (!sockets[index].HasValue) return new GemResult { Status = GemResultStatus.EmptySocket, ItemName = NameOf(item), Socket = index + 1, SocketCount = count };

            var result = new GemResult { Status = GemResultStatus.Ok, ItemName = NameOf(item), Socket = index + 1, SocketCount = count };
            var target = inventory.Get(item, true);
            try
            {
                Remove(character, inventory, sockets[index].Value, result);
                sockets[index] = null;
                SetSockets(inventory, target, Gems.FormatSockets(sockets));
                return result;
            }
            finally
            {
                inventory.Unlock(item.Id);
            }
        }

        /// <summary>The chance this character has to keep a gem taken out: the clan's Enchanting and the Gem Salvager node.</summary>
        public double KeepChance(Character character)
        {
            var build = character != null ? gameData.GetCharacterSkillBuild(character.Id) : null;
            var salvager = build != null ? RavenNest.Combat.SkillBuild.Parse(build.Build).Rank("gem-salvager") : 0;
            return Gems.KeepChance(ClanEnchantingLevel(character), salvager);
        }

        private void Remove(Character character, PlayerInventory inventory, Guid gemId, GemResult result)
        {
            if (!Gems.TryGetGem(gemId, out var kind, out var tier)) return;
            result.OldKind = (int)kind;
            result.OldTier = (int)tier;
            result.KeepChance = KeepChance(character);
            result.Kept = random.NextDouble() < result.KeepChance;
            if (result.Kept) AddGem(inventory, kind, tier, 1);
        }

        // an item of more than one splits: one of them gets the gems, the rest stay a plain stack
        private void SetSockets(PlayerInventory inventory, DataModels.InventoryItem stack, string sockets)
        {
            if (stack == null) return;
            if (stack.Amount.GetValueOrDefault(1) <= 1)
            {
                stack.Sockets = sockets;
                return;
            }

            if (!inventory.RemoveItem(stack, 1)) return;
            var single = inventory.AddItemStack(stack, 1);
            single.Equipped = false;
            single.Sockets = sockets;
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
                    return type != ItemType.Hat && type != ItemType.Mask && type != ItemType.HeadCovering;
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
                    return item.Type != RavenNest.Models.ItemType.Hat && item.Type != RavenNest.Models.ItemType.Mask && item.Type != RavenNest.Models.ItemType.HeadCovering;
            }
            return false;
        }

        /// <summary>An item's sockets for showing: ("Socket 1", "Flawless Ruby: +18% Fire Damage") or ("Socket 2", "empty").</summary>
        public static System.Collections.Generic.List<(string Label, string Value)> SocketLines(RavenNest.Models.Item item, string sockets)
        {
            var lines = new System.Collections.Generic.List<(string, string)>();
            if (!TryGetSlot(item, out var slot)) return lines;
            var count = Gems.Sockets(Gems.ItemLevel(item.RequiredAttackLevel, item.RequiredDefenseLevel, item.RequiredMagicLevel, item.RequiredRangedLevel, item.RequiredSlayerLevel, item.Level), slot);
            var parsed = Gems.ParseSockets(sockets, count);
            for (var s = 0; s < parsed.Length; ++s)
            {
                var value = parsed[s].HasValue && Gems.TryGetGem(parsed[s].Value, out var kind, out var tier)
                    ? Gems.Name(kind, tier) + ": " + Gems.Describe(kind, tier, slot)
                    : "empty";
                lines.Add(("Socket " + (s + 1), value));
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

        private static bool Valid(int kind) => kind >= 0 && kind < Gems.KindCount;

        private Character GetCharacter(SessionToken token, Guid characterId)
        {
            var session = gameData.GetSession(token.SessionId);
            if (session == null) return null;
            return gameData.GetActiveSessionCharacters(session)?.FirstOrDefault(x => x.Id == characterId);
        }
    }
}

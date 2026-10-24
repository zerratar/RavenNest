using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RavenNest.BusinessLogic.Game;
using RavenNest.Models;
using RavenNest.Protocol;

namespace RavenNest.BusinessLogic.Net.V2
{
    /// <summary>
    /// What a command handler gets: the session it runs for, and <see cref="Watch"/> to name the characters whose
    /// inventories it may change, so the result can carry those changes.
    /// </summary>
    public sealed class CommandContext
    {
        private readonly PlayerInventoryProvider inventories;
        internal readonly Dictionary<Guid, Dictionary<Guid, InventoryStack>> Before = new Dictionary<Guid, Dictionary<Guid, InventoryStack>>();

        public CommandContext(SessionToken sessionToken, PlayerInventoryProvider inventories)
        {
            SessionToken = sessionToken;
            this.inventories = inventories;
        }

        public SessionToken SessionToken { get; }

        /// <summary>Takes a snapshot of the character's inventory now, to compare with after the command.</summary>
        public void Watch(Guid characterId)
        {
            if (characterId == Guid.Empty || Before.ContainsKey(characterId))
            {
                return;
            }
            Before[characterId] = Snapshot(inventories, characterId);
        }

        internal static Dictionary<Guid, InventoryStack> Snapshot(PlayerInventoryProvider inventories, Guid characterId)
        {
            var result = new Dictionary<Guid, InventoryStack>();
            var inventory = inventories.Get(characterId);
            if (inventory == null)
            {
                return result;
            }

            foreach (var item in inventory.GetAllItems())
            {
                result[item.Id] = new InventoryStack
                {
                    Id = item.Id,
                    ItemId = item.ItemId,
                    Amount = item.Amount,
                    Equipped = item.Equipped,
                    Soulbound = item.Soulbound,
                    Name = item.Name,
                    Enchantment = item.Enchantment,
                    Tag = item.Tag,
                    TransmogrificationId = item.TransmogrificationId,
                    Flags = item.Flags
                };
            }
            return result;
        }
    }

    /// <summary>
    /// Runs the commands of <see cref="Commands"/>: each reads its arguments, names the inventories it touches and
    /// calls the same PlayerManager method its REST endpoint calls, so the rules are the same on both paths.
    /// </summary>
    public sealed class CommandDispatcher
    {
        private delegate Task<object> Handler(CommandContext context, byte[] args);

        private readonly Dictionary<string, Handler> handlers = new Dictionary<string, Handler>(StringComparer.Ordinal);
        private readonly PlayerInventoryProvider inventories;

        public CommandDispatcher(PlayerManager players, PlayerInventoryProvider inventories, Game.StreamerShop.StreamerShopManager shop)
        {
            this.inventories = inventories;
            var p = players;

            // joining and leaving
            Add(Commands.PlayerJoin, async (c, a) => { CommandArgs.Unpack(a, out PlayerJoinData d); return await p.AddPlayer(c.SessionToken, d); });
            Add(Commands.PlayerRestore, async (c, a) => { CommandArgs.Unpack(a, out PlayerRestoreData d); return await p.RestorePlayersToGame(c.SessionToken, d); });
            Add(Commands.PlayerRemove, async (c, a) => { CommandArgs.Unpack(a, out Guid ch); return await p.RemovePlayerFromActiveSession(c.SessionToken, ch); });
            Add(Commands.PlayerRemoveFailed, async (c, a) => { CommandArgs.Unpack(a, out Guid ch, out string reason); return await p.PlayerRemoveFailed(c.SessionToken, ch, reason); });

            // items
            Add(Commands.AddItem, (c, a) => { CommandArgs.Unpack(a, out Guid ch, out Guid item); c.Watch(ch); return p.AddItem(c.SessionToken, ch, item, out _); });
            Add(Commands.AddItemInstance, (c, a) => { CommandArgs.Unpack(a, out Guid ch, out AddItemRequest item); c.Watch(ch); return p.AddItem(c.SessionToken, ch, item); });
            Add(Commands.Produce, (c, a) => { CommandArgs.Unpack(a, out Guid ch, out Guid recipe, out long amount); c.Watch(ch); return p.ProduceItems(c.SessionToken, ch, recipe, (int)Math.Clamp(amount, 0, int.MaxValue)); });
            Add(Commands.Craft, (c, a) => { CommandArgs.Unpack(a, out Guid ch, out Guid item, out int amount); c.Watch(ch); return p.CraftItems(c.SessionToken, ch, item, amount); });
            Add(Commands.Redeem, (c, a) => { CommandArgs.Unpack(a, out Guid ch, out Guid item); c.Watch(ch); return p.RedeemItem(c.SessionToken, ch, item); });
            Add(Commands.ShopBuy, (c, a) => { CommandArgs.Unpack(a, out Guid ch, out Guid listing, out long amount); c.Watch(ch); return shop.Buy(c.SessionToken, ch, listing, amount); });
            Add(Commands.UseItem, (c, a) => { CommandArgs.Unpack(a, out Guid ch, out Guid inv, out string arg); c.Watch(ch); return p.UseItem(c.SessionToken, ch, inv, arg); });
            Add(Commands.Vendor, (c, a) => { CommandArgs.Unpack(a, out Guid ch, out Guid inv, out long amount); c.Watch(ch); return p.SellItemInstanceToVendor(c.SessionToken, ch, inv, amount); });
            Add(Commands.Gift, (c, a) => { CommandArgs.Unpack(a, out Guid ch, out Guid receiver, out Guid inv, out long amount); c.Watch(ch); return p.GiftInventoryItem(c.SessionToken, ch, receiver, inv, amount); });
            Add(Commands.SendItem, (c, a) => { CommandArgs.Unpack(a, out Guid ch, out string alias, out Guid inv, out long amount); c.Watch(ch); return p.SendInventoryItem(c.SessionToken, ch, alias, inv, amount); });
            Add(Commands.SendCoins, (c, a) => { CommandArgs.Unpack(a, out Guid from, out Guid to, out long amount); return p.SendCoins(c.SessionToken, from, to, amount); });
            Add(Commands.AddTokens, (c, a) => { CommandArgs.Unpack(a, out Guid ch, out int amount); return p.AddTokens(c.SessionToken, ch, amount); });

            // equipment
            Add(Commands.Equip, (c, a) => { CommandArgs.Unpack(a, out Guid ch, out Guid inv); c.Watch(ch); return p.EquipItemInstance(c.SessionToken, ch, inv); });
            Add(Commands.EquipItem, (c, a) => { CommandArgs.Unpack(a, out Guid ch, out Guid item); c.Watch(ch); return p.EquipItem(c.SessionToken, ch, item); });
            Add(Commands.EquipBest, (c, a) => { CommandArgs.Unpack(a, out Guid ch); c.Watch(ch); return p.EquipBestItems(c.SessionToken, ch); });
            Add(Commands.Unequip, (c, a) => { CommandArgs.Unpack(a, out Guid ch, out Guid inv); c.Watch(ch); return p.UnequipItemInstance(c.SessionToken, ch, inv); });
            Add(Commands.UnequipAll, (c, a) => { CommandArgs.Unpack(a, out Guid ch); c.Watch(ch); return p.UnequipAllItems(c.SessionToken, ch); });
            Add(Commands.ToggleHelmet, (c, a) => { CommandArgs.Unpack(a, out Guid ch); c.Watch(ch); return p.ToggleHelmet(c.SessionToken, ch); });

            // enchanting
            Add(Commands.Enchant, (c, a) => { CommandArgs.Unpack(a, out Guid ch, out Guid inv); c.Watch(ch); return p.EnchantItemInstance(c.SessionToken, ch, inv); });
            Add(Commands.Disenchant, (c, a) => { CommandArgs.Unpack(a, out Guid ch, out Guid inv); c.Watch(ch); return p.DisenchantItemInstance(c.SessionToken, ch, inv); });
            Add(Commands.EnchantmentCooldown, (c, a) => { CommandArgs.Unpack(a, out Guid ch); return p.GetEnchantmentCooldown(c.SessionToken, ch); });
            Add(Commands.ClearEnchantmentCooldown, (c, a) => { CommandArgs.Unpack(a, out Guid ch); c.Watch(ch); return p.ClearEnchantmentCooldown(c.SessionToken, ch); });

            // raids and dungeons
            Add(Commands.AutoJoinRaid, (c, a) => { CommandArgs.Unpack(a, out Guid[] ids); return p.AutoJoinRaid(c.SessionToken, ids ?? Array.Empty<Guid>()); });
            Add(Commands.AutoJoinDungeon, (c, a) => { CommandArgs.Unpack(a, out Guid[] ids); return p.AutoJoinDungeon(c.SessionToken, ids ?? Array.Empty<Guid>()); });
            Add(Commands.AutoJoinRaidCost, (c, a) => PlayerManager.AutoJoinRaidCost);

            // other
            Add(Commands.Highscore, (c, a) => { CommandArgs.Unpack(a, out Guid ch, out string skill); return p.GetHighscore(c.SessionToken, ch, skill); });
            Add(Commands.LoyaltyUpdate, (c, a) => { CommandArgs.Unpack(a, out LoyaltyUpdate d); return p.AddLoyaltyData(c.SessionToken, d); });
        }

        public bool Knows(string command) => command != null && handlers.ContainsKey(command);

        public IReadOnlyCollection<string> Names => handlers.Keys;

        private void Add(string name, Func<CommandContext, byte[], object> handler)
        {
            handlers[name] = (c, a) => Task.FromResult(handler(c, a));
        }

        private void Add(string name, Func<CommandContext, byte[], Task<object>> handler)
        {
            handlers[name] = (c, a) => handler(c, a);
        }

        /// <summary>
        /// Runs a command. Never throws: failures become a result with a status. The inventory changes are those of
        /// the characters the handler watched, numbered by <paramref name="nextRevision"/>.
        /// </summary>
        public async Task<CommandResult> RunAsync(SessionToken sessionToken, CommandRequest request, Func<Guid, long> nextRevision)
        {
            var result = new CommandResult { RequestId = request.RequestId };
            if (request.Command == null || !handlers.TryGetValue(request.Command, out var handler))
            {
                result.Status = CommandStatus.UnknownCommand;
                result.Error = "Unknown command '" + request.Command + "'.";
                return result;
            }

            var context = new CommandContext(sessionToken, inventories);
            object value;
            try
            {
                value = await handler(context, request.Args);
            }
            catch (MessagePack.MessagePackSerializationException exc)
            {
                result.Status = CommandStatus.BadArguments;
                result.Error = "The arguments for '" + request.Command + "' could not be read: " + exc.Message;
                return result;
            }
            catch (Exception exc)
            {
                result.Status = CommandStatus.Error;
                result.Error = exc.Message;
                result.Inventory = Changes(context, nextRevision);
                throw new CommandFailedException(result, exc);
            }

            result.Status = CommandStatus.Ok;
            if (value != null)
            {
                result.Value = MessagePack.MessagePackSerializer.Serialize(value.GetType(), value, ProtocolSerializer.Values);
            }
            result.Inventory = Changes(context, nextRevision);
            return result;
        }

        private InventoryUpdate[] Changes(CommandContext context, Func<Guid, long> nextRevision)
        {
            if (context.Before.Count == 0)
            {
                return null;
            }

            List<InventoryUpdate> updates = null;
            foreach (var (characterId, before) in context.Before)
            {
                var after = CommandContext.Snapshot(inventories, characterId);
                var changed = new List<InventoryStack>();
                foreach (var (id, stack) in after)
                {
                    if (!before.TryGetValue(id, out var old) || !Same(old, stack))
                    {
                        changed.Add(stack);
                    }
                }

                foreach (var (id, old) in before)
                {
                    if (!after.ContainsKey(id))
                    {
                        changed.Add(new InventoryStack { Id = id, ItemId = old.ItemId, Amount = 0 });
                    }
                }

                if (changed.Count == 0)
                {
                    continue;
                }

                (updates ??= new List<InventoryUpdate>()).Add(new InventoryUpdate
                {
                    CharacterId = characterId,
                    Revision = nextRevision(characterId),
                    Stacks = changed.ToArray()
                });
            }
            return updates?.ToArray();
        }

        private static bool Same(InventoryStack a, InventoryStack b)
        {
            return a.ItemId == b.ItemId && a.Amount == b.Amount && a.Equipped == b.Equipped && a.Soulbound == b.Soulbound
                && a.Name == b.Name && a.Enchantment == b.Enchantment && a.Tag == b.Tag
                && a.TransmogrificationId == b.TransmogrificationId && a.Flags == b.Flags;
        }
    }

    /// <summary>A command threw; carries the result to send (status Error) so the caller can log and still answer.</summary>
    public sealed class CommandFailedException : Exception
    {
        public CommandFailedException(CommandResult result, Exception inner) : base(inner.Message, inner)
        {
            Result = result;
        }

        public CommandResult Result { get; }
    }
}

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using RavenNest.BusinessLogic.AI;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Extended;
using RavenNest.BusinessLogic.Game;
using RavenNest.BusinessLogic.Settings;
using RavenNest.BusinessLogic;

namespace RavenNest.Blazor.Services.Assistant
{
    /// <summary>
    ///     The assistant a player can talk to, and the only place its tools are built.
    /// </summary>
    /// <remarks>
    ///     Every tool here is built for one signed in user and closes over that user's id. The model
    ///     never sees an id and has no argument through which it could name one: it refers to
    ///     characters by name or by number, and the handler resolves that against the characters
    ///     that user owns. So there is nothing to say to the assistant that reaches another player's
    ///     things, whatever it is persuaded of.
    ///
    ///     <para>
    ///     The one tool that changes anything goes through the same PlayerManager call the Send
    ///     button on the inventory page uses. It is not a second route into moving items with its
    ///     own rules; it is the button, pressed by a different finger. Anything that method refuses
    ///     it still refuses here, including the same user check on both characters.
    ///     </para>
    ///
    ///     <para>
    ///     That tool needs confirming, so it stops and asks. The read only ones do not, and the
    ///     difference is declared by the tool rather than decided by the model or by the caller.
    ///     </para>
    /// </remarks>
    public class PlayerAssistant
    {
        /// <summary>
        ///     How many questions one person may ask in a day.
        /// </summary>
        /// <remarks>
        ///     Somebody else is paying for this key, and an assistant with no ceiling is a bill with
        ///     no ceiling. Counted in memory, so a restart forgives everybody: this is a cost guard
        ///     rather than a rule anyone is meant to be punished by, and a restart is rare enough
        ///     that the leak does not matter.
        /// </remarks>
        private const int DefaultDailyLimit = 30;

        private static readonly ConcurrentDictionary<Guid, Usage> usage = new ConcurrentDictionary<Guid, Usage>();

        private readonly IAiService ai;
        private readonly GameData gameData;
        private readonly PlayerManager playerManager;
        private readonly MarketPriceIndex marketPrices;
        private readonly IServerSettingsProvider settings;

        public PlayerAssistant(
            IAiService ai,
            GameData gameData,
            PlayerManager playerManager,
            MarketPriceIndex marketPrices,
            IServerSettingsProvider settings)
        {
            this.ai = ai;
            this.gameData = gameData;
            this.playerManager = playerManager;
            this.marketPrices = marketPrices;
            this.settings = settings;
        }

        /// <summary>
        ///     Whether this person can use it. Needs a key either way.
        /// </summary>
        /// <remarks>
        ///     Administrators do not wait for the switch. Somebody has to try the thing before
        ///     deciding whether to turn it on for everyone, and the only way to do that otherwise
        ///     would be to turn it on for everyone.
        /// </remarks>
        public bool IsAvailableTo(bool isAdministrator)
        {
            if (!ai.IsConfigured) return false;
            return isAdministrator || settings.GetToggle(ServerSettingsRegistry.AssistantEnabled);
        }

        /// <summary>
        ///     Whether players other than administrators have it yet.
        /// </summary>
        public bool IsOnForEveryone => settings.GetToggle(ServerSettingsRegistry.AssistantEnabled);

        public int DailyLimit
        {
            get
            {
                var configured = settings.GetNumber(ServerSettingsRegistry.AssistantDailyLimit, DefaultDailyLimit);
                return configured <= 0 ? DefaultDailyLimit : (int)configured;
            }
        }

        public int AskedToday(Guid userId) => Today(userId).Count;

        public int RemainingToday(Guid userId) => Math.Max(0, DailyLimit - AskedToday(userId));

        /// <summary>
        ///     Counts one question against the day's allowance. Returns false when there is none
        ///     left, and counts nothing in that case.
        /// </summary>
        public bool TryUse(Guid userId)
        {
            var today = Today(userId);
            if (today.Count >= DailyLimit) return false;

            today.Count++;
            return true;
        }

        public AiConversation StartFor(Guid userId)
        {
            return new AiConversation(ai, InstructionsFor(userId), ToolsFor(userId), maxOutputTokens: 2000);
        }

        private string InstructionsFor(Guid userId)
        {
            var user = gameData.GetUser(userId);

            return @"
You help a player of Ravenfall with their own characters. Ravenfall is played through chat commands
while a streamer runs the game.

You are talking to " + (user?.UserName ?? "a player") + @". Everything you can see belongs to them.

How to answer:
- Short. A sentence or two, unless they asked for a list.
- Plain words, no hype, no emoji, no exclamation marks.
- Never use em dashes or en dashes.
- Never guess at a number. If you have not looked it up with a tool, look it up or say you do not
  know. A made up coin total or item count is worse than no answer.
- You cannot see other players. If asked about somebody else, say so.
- You cannot change anything except by moving items between their own characters, and that always
  has to be agreed to first. Never say you have done something you have only proposed.
- If they ask for something you have no tool for, say what you cannot do rather than approximating
  it. Guessing which item is best without checking their skills is exactly the kind of answer that
  reads as authoritative and is not.

Some questions arrive with a line saying which page the person is looking at. Use it to work out
what a vague question means: on the vendor page, ""is this worth it"" is about the vendor, and on a
character page, ""what should I train"" is about that character. It tells you where they are, not
what is on their screen, so still look things up with a tool rather than assuming. Do not mention
the page back to them unless it matters to the answer.
";
        }

        private IReadOnlyList<AiTool> ToolsFor(Guid userId)
        {
            return new List<AiTool>
            {
                new AiTool(
                    "my_characters",
                    "The player's characters, with their number, name, combat level and coins. " +
                    "Call this before anything that names a character.",
                    AiTool.NoParameters,
                    (args, ct) => Task.FromResult(Characters(userId))),

                new AiTool(
                    "character_items",
                    "What one of the player's characters is carrying. Names, amounts, whether each " +
                    "is equipped, and whether it is soulbound.",
                    Schema("character", "The character's name or number, as given by my_characters."),
                    (args, ct) => Task.FromResult(Inventory(userId, Text(args, "character")))),

                new AiTool(
                    "vendor_stock",
                    "What the vendor has for sale, with the price to buy one and how many are left. " +
                    "Optionally filtered to names containing a word.",
                    Schema("search", "A word to filter item names by, or null for everything.", nullable: true),
                    (args, ct) => Task.FromResult(Vendor(Text(args, "search")))),

                // The only one that changes anything, and the reason the confirmation machinery
                // exists. Everything above it reads.
                new AiTool(
                    "move_items",
                    "Move items from one of the player's characters to another of their own " +
                    "characters. This has to be agreed to by the player before it happens.",
                    MoveSchema,
                    (args, ct) => Task.FromResult(Move(userId, args)),
                    requiresConfirmation: true,
                    summarise: args => DescribeMove(userId, args))
            };
        }

        // ---- what the tools return -----------------------------------------------------------

        private string Characters(Guid userId)
        {
            var players = playerManager.GetWebsitePlayers(userId);
            if (players == null || players.Count == 0) return "This player has no characters.";

            return Json(players.Select(p => new
            {
                number = p.CharacterIndex,
                name = p.Name,
                alias = p.Alias,
                combatLevel = p.CombatLevel,
                coins = (long)(p.Resources?.Coins ?? 0)
            }));
        }

        private string Inventory(Guid userId, string which)
        {
            var character = Resolve(userId, which);
            if (character == null) return NoSuchCharacter(userId, which);

            var items = character.InventoryItems;
            if (items == null || items.Count == 0) return character.Name + " is carrying nothing.";

            return Json(items.Select(i => new
            {
                name = ItemName(i),
                amount = i.Amount,
                equipped = i.Equipped,
                soulbound = i.Soulbound,
                enchanted = !string.IsNullOrEmpty(i.Enchantment)
            }));
        }

        private string Vendor(string search)
        {
            var rows = new List<object>();

            foreach (var stocked in gameData.GetVendorItems())
            {
                var item = gameData.GetItem(stocked.ItemId);

                // Soulbound stock is never sold, so listing it would be advertising something the
                // player cannot buy.
                if (item == null || item.Soulbound) continue;

                if (!string.IsNullOrWhiteSpace(search) &&
                    item.Name?.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                rows.Add(new
                {
                    name = item.Name,
                    price = GameMath.CalculateVendorBuyPrice(item, stocked.Stock, marketPrices.GetAnchor(item.Id)),
                    inStock = stocked.Stock,
                    requiredLevel = item.RequiredAttackLevel > 0 ? item.RequiredAttackLevel : item.RequiredDefenseLevel
                });

                // The whole shop would be a lot of tokens to no purpose. Enough to answer a
                // question about, and the model can filter again by asking with a search word.
                if (rows.Count >= 60) break;
            }

            return rows.Count == 0 ? "The vendor has nothing matching that." : Json(rows);
        }

        // ---- the one that changes something ---------------------------------------------------

        /// <summary>
        ///     What the player is being asked to agree to. Written from the arguments rather than
        ///     from anything the model wrote, so the sentence on the button is the action that will
        ///     actually run.
        /// </summary>
        private string DescribeMove(Guid userId, JsonElement args)
        {
            var from = Resolve(userId, Text(args, "from"));
            var to = Resolve(userId, Text(args, "to"));
            var item = Text(args, "item");
            var amount = Number(args, "amount");

            var fromName = from?.Name ?? Text(args, "from");
            var toName = to?.Name ?? Text(args, "to");

            return "Move " + amount + "x " + item + " from " + fromName + " to " + toName + ".";
        }

        private string Move(Guid userId, JsonElement args)
        {
            var from = Resolve(userId, Text(args, "from"));
            if (from == null) return NoSuchCharacter(userId, Text(args, "from"));

            var to = Resolve(userId, Text(args, "to"));
            if (to == null) return NoSuchCharacter(userId, Text(args, "to"));

            if (from.Id == to.Id) return "Those are the same character. Nothing to move.";

            var amount = Number(args, "amount");
            if (amount <= 0) return "The amount has to be more than zero.";

            var wanted = Text(args, "item");
            var match = (from.InventoryItems ?? Array.Empty<RavenNest.Models.InventoryItem>())
                .Where(i => !i.Equipped)
                .FirstOrDefault(i => string.Equals(ItemName(i), wanted, StringComparison.OrdinalIgnoreCase))
                ?? (from.InventoryItems ?? Array.Empty<RavenNest.Models.InventoryItem>())
                    .Where(i => !i.Equipped)
                    .FirstOrDefault(i => ItemName(i).IndexOf(wanted ?? "", StringComparison.OrdinalIgnoreCase) >= 0);

            if (match == null)
            {
                return from.Name + " has no unequipped '" + wanted + "'. Unequip it first if it is worn.";
            }

            if (amount > match.Amount)
            {
                return from.Name + " only has " + match.Amount + " of those.";
            }

            // The same call the Send button on the inventory page makes, which checks that both
            // characters belong to the same user and puts the items back if the delivery fails.
            if (!playerManager.SendToCharacter(from.Id, to.Id, match, amount))
            {
                return "The move did not go through. Nothing was taken from " + from.Name + ".";
            }

            return "Moved " + amount + "x " + ItemName(match) + " from " + from.Name + " to " + to.Name + ".";
        }

        // ---- resolving a character, which is where the scoping is enforced --------------------

        /// <summary>
        ///     Turns whatever the model called a character into one of this user's characters, or
        ///     nothing.
        /// </summary>
        /// <remarks>
        ///     The list is fetched for the signed in user and matched within it, so a name that
        ///     belongs to somebody else simply does not resolve. There is no branch here that can be
        ///     talked into widening the search.
        /// </remarks>
        private WebsitePlayer Resolve(Guid userId, string which)
        {
            if (string.IsNullOrWhiteSpace(which)) return null;

            var players = playerManager.GetWebsitePlayers(userId);
            if (players == null) return null;

            which = which.Trim();

            var byName = players.FirstOrDefault(p => string.Equals(p.Name, which, StringComparison.OrdinalIgnoreCase))
                      ?? players.FirstOrDefault(p => string.Equals(p.Alias, which, StringComparison.OrdinalIgnoreCase));
            if (byName != null) return byName;

            // "character 2", "#2", "2".
            var digits = new string(which.Where(char.IsDigit).ToArray());
            if (digits.Length > 0 && int.TryParse(digits, out var index))
            {
                return players.FirstOrDefault(p => p.CharacterIndex == index);
            }

            return null;
        }

        private string NoSuchCharacter(Guid userId, string which)
        {
            var players = playerManager.GetWebsitePlayers(userId);
            var names = players == null ? "" : string.Join(", ", players.Select(p => p.Name));

            return "There is no character called '" + which + "'. This player has: " + names + ".";
        }

        // ---- odds and ends ---------------------------------------------------------------------

        /// <summary>
        ///     What to call the item. InventoryItem.Name only carries something when the instance
        ///     has been given one, so the item's own name is the usual answer.
        /// </summary>
        private string ItemName(RavenNest.Models.InventoryItem item)
        {
            if (!string.IsNullOrWhiteSpace(item.Name)) return item.Name;
            return gameData.GetItem(item.ItemId)?.Name ?? "unknown item";
        }

        private static string Json(object value) =>
            JsonSerializer.Serialize(value);

        private static string Text(JsonElement args, string name)
        {
            if (args.ValueKind != JsonValueKind.Object) return null;
            if (!args.TryGetProperty(name, out var value)) return null;
            return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        }

        private static long Number(JsonElement args, string name)
        {
            if (args.ValueKind != JsonValueKind.Object) return 0;
            if (!args.TryGetProperty(name, out var value)) return 0;

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number)) return number;
            if (value.ValueKind == JsonValueKind.String && long.TryParse(value.GetString(), out var parsed)) return parsed;

            return 0;
        }

        /// <summary>
        ///     A one property schema. Strict mode wants every property listed as required and no
        ///     extras allowed, so an optional one is spelled as allowing null rather than by being
        ///     left out.
        /// </summary>
        private static string Schema(string name, string description, bool nullable = false)
        {
            var type = nullable ? "[\"string\",\"null\"]" : "\"string\"";

            return "{\"type\":\"object\",\"properties\":{\"" + name + "\":{\"type\":" + type +
                   ",\"description\":\"" + description + "\"}},\"required\":[\"" + name +
                   "\"],\"additionalProperties\":false}";
        }

        private const string MoveSchema =
            "{\"type\":\"object\",\"properties\":{" +
            "\"from\":{\"type\":\"string\",\"description\":\"The character to take the items from, by name or number.\"}," +
            "\"to\":{\"type\":\"string\",\"description\":\"The character to give them to, by name or number. Must be another of the same player's characters.\"}," +
            "\"item\":{\"type\":\"string\",\"description\":\"The item's name, exactly as character_items reported it.\"}," +
            "\"amount\":{\"type\":\"integer\",\"description\":\"How many to move.\"}}," +
            "\"required\":[\"from\",\"to\",\"item\",\"amount\"],\"additionalProperties\":false}";

        private static Usage Today(Guid userId)
        {
            var today = DateTime.UtcNow.Date;
            var current = usage.GetOrAdd(userId, _ => new Usage { Day = today });

            if (current.Day != today)
            {
                current.Day = today;
                current.Count = 0;
            }

            return current;
        }

        private sealed class Usage
        {
            public DateTime Day;
            public int Count;
        }
    }
}

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
using RavenNest.BusinessLogic.Net;
using RavenNest.BusinessLogic.Settings;
using RavenNest.Blazor.Services.Knowledge;
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

        /// <summary>
        ///     The conversation each person is in the middle of.
        /// </summary>
        /// <remarks>
        ///     Held here rather than in the chat widget, because the widget dies with the page. A
        ///     reload used to lose the whole exchange, which is a strange thing for a conversation to
        ///     do and easy to mistake for the assistant having forgotten.
        ///
        ///     <para>
        ///     One per person rather than one per tab, so two tabs share it. That is the honest
        ///     reading of "carry on where I left off", and it is the same conversation on the other
        ///     side regardless: the history lives at OpenAI against a response id, and a second copy
        ///     would only be a second view of it.
        ///     </para>
        ///
        ///     <para>
        ///     In memory, so a restart clears them. Losing an exchange to a deploy is a much smaller
        ///     thing than storing everybody's chat history, which nobody asked for.
        ///     </para>
        /// </remarks>
        private static readonly ConcurrentDictionary<Guid, AiConversation> conversations =
            new ConcurrentDictionary<Guid, AiConversation>();

        private readonly IAiService ai;
        private readonly GameData gameData;
        private readonly PlayerManager playerManager;
        private readonly MarketPriceIndex marketPrices;
        private readonly ServerService serverService;
        private readonly FactService facts;
        private readonly GameActions gameActions;
        private readonly PlayerService playerService;
        private readonly BotService botService;
        private readonly TownService townService;
        private readonly IServerSettingsProvider settings;

        public PlayerAssistant(
            IAiService ai,
            GameData gameData,
            PlayerManager playerManager,
            MarketPriceIndex marketPrices,
            ServerService serverService,
            FactService facts,
            GameActions gameActions,
            PlayerService playerService,
            BotService botService,
            TownService townService,
            IServerSettingsProvider settings)
        {
            this.ai = ai;
            this.gameData = gameData;
            this.playerManager = playerManager;
            this.marketPrices = marketPrices;
            this.serverService = serverService;
            this.facts = facts;
            this.gameActions = gameActions;
            this.playerService = playerService;
            this.botService = botService;
            this.townService = townService;
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
        ///     Counts one question against the day, and says whether it is allowed.
        /// </summary>
        /// <remarks>
        ///     Administrators are counted but never stopped. The limit exists to keep the bill from
        ///     running away, and the person who owns the key is the one paying that bill and the one
        ///     who can change the number; stopping them at thirty is friction with nothing on the
        ///     other side of it. They still get counted, because what they have spent today is worth
        ///     showing to the person spending it.
        /// </remarks>
        public bool TryUse(Guid userId, bool isAdministrator)
        {
            var today = Today(userId);
            if (!isAdministrator && today.Count >= DailyLimit) return false;

            today.Count++;
            return true;
        }

        /// <param name="isAdministrator">
        ///     From the signed in session and nowhere else. It decides which tools exist at all, so a
        ///     conversation that was not given the admin tools has no route to them: they are absent
        ///     from the request, and a name the model invents resolves to nothing.
        /// </param>
        public AiConversation StartFor(Guid userId, bool isAdministrator, bool isModerator = false)
        {
            return conversations.GetOrAdd(userId, _ => Build(userId, isAdministrator, isModerator));
        }

        /// <summary>
        ///     Throws the exchange away and starts again.
        /// </summary>
        /// <remarks>
        ///     Drops the response id along with the turns, so the next question genuinely starts
        ///     from nothing rather than looking empty while the model still remembers. A clear that
        ///     only clears the screen is worse than none, because it is the one thing you reach for
        ///     when the assistant has got hold of the wrong end of something.
        /// </remarks>
        public AiConversation Restart(Guid userId, bool isAdministrator, bool isModerator = false)
        {
            var fresh = Build(userId, isAdministrator, isModerator);
            conversations[userId] = fresh;
            return fresh;
        }

        private AiConversation Build(Guid userId, bool isAdministrator, bool isModerator)
        {
            // Created here and closed over by the tools, because the tools are built before the
            // conversation exists and both need to be looking at the same list.
            var offers = new List<AiOffer>();

            return new AiConversation(
                ai,
                InstructionsFor(userId, isAdministrator),
                ToolsFor(userId, isAdministrator, isModerator, offers),
                maxOutputTokens: 2000,
                offers: offers);
        }

        private string InstructionsFor(Guid userId, bool isAdministrator)
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
- Coins and resources belong to the account, not to a character. There is one figure for the whole
  account. Never add them up per character and never say a character has its own coins.
- You can move items between their own characters, and tell the running game to change what a
  character is training, sail somewhere, rest, or join a raid or dungeon. All of those have to be
  agreed to first. Never say you have done something you have only proposed.
- If they own a town you can also change it: build every plot as one house type, change one plot, or
  decide who lives in a plot. Read it with my_town first, and refer to plots by the number my_town
  gives. Building every plot replaces every assignment in the town, so make sure they know that is
  what they are agreeing to.
- Whether the chat bot is in their channel is something you can check with my_bot_status. Use it for
  any question about the bot being connected or commands not working, rather than the server wide
  channel count, which says nothing about their channel.
- Those game actions only reach a character that is in a live stream, and they are sent to the game
  rather than done here. Say it has been told to do something, not that it is now doing it, because
  the game decides what actually happens.
- If they ask for something you have no tool for, say what you cannot do rather than approximating
  it. Guessing which item is best without checking their skills is exactly the kind of answer that
  reads as authoritative and is not.
- When your answer points at somewhere on the site, add a button with offer_link. Telling somebody
  they can do it on their stash page is a worse answer than the same sentence with a button under
  it. If they ask to be taken somewhere, offer the link and say what they will find there, rather
  than explaining that you cannot navigate for them. When the place is one of their characters, use
  offer_character_link instead and name the tab, so the button lands on the right character and the
  right tab rather than on the character list.
- Anything about where a character is, what it is wearing, or what an item does: use the tools.
  Those are facts about right now and guessing at them is always wrong.
- Anything about how the game works, what something costs, or what a mechanic does: look it up with
  search_knowledge first. Do not explain a mechanic from memory. If nothing is written down, say so
  and offer to remember what they tell you, rather than reasoning your way to something plausible.
- When somebody corrects you, use remember_correction, and then say plainly what happened to it.
  Never say you have learned something when it has only been sent for review.

" + (isAdministrator ? AdminVoice : PlayerVoice) + @"

Some questions arrive with a line saying which page the person is looking at. Use it to work out
what a vague question means: on the vendor page, ""is this worth it"" is about the vendor, and on a
character page, ""what should I train"" is about that character. It tells you where they are, not
what is on their screen, so still look things up with a tool rather than assuming. Do not mention
the page back to them unless it matters to the answer.
";
        }

        /// <summary>
        ///     What a player is told about the edges of what it can see.
        /// </summary>
        private const string PlayerVoice =
            "- You cannot see other players, the server, or any characters but theirs. If asked about\n" +
            "  somebody else, say so plainly rather than guessing.";

        /// <summary>
        ///     What an administrator is told instead.
        /// </summary>
        /// <remarks>
        ///     The instructions have to match the tools. An administrator with the server tools who
        ///     is still told they cannot see other players will refuse things it can actually do,
        ///     which is exactly how the gap got noticed: asked for the live player count while the
        ///     number was on the screen behind it, and it said it could not see.
        /// </remarks>
        private const string AdminVoice =
            "- This person is an administrator. You can also see how the server is doing right now, which\n" +
            "  streams are live, and look up any player by name. Reach for those tools rather than saying\n" +
            "  you cannot see other players, because for this person you can.\n" +
            "- You still cannot change anything belonging to another player. Looking is all you can do\n" +
            "  there, and moving items is still only between this person's own characters.";

        private IReadOnlyList<AiTool> ToolsFor(Guid userId, bool isAdministrator, bool isModerator, IList<AiOffer> offers)
        {
            var tools = new List<AiTool>
            {
                new AiTool(
                    "my_characters",
                    "The player's characters: number, name, combat level, which one is the main, and " +
                    "what each is doing right now. Call this before anything that names a character. " +
                    "Coins are not here because they are not per character, see my_account.",
                    AiTool.NoParameters,
                    (args, ct) => Task.FromResult(Characters(userId))),

                // Coins and resources belong to the account, not to a character. Their own tool, so
                // there is no per character number lying around to be added up.
                new AiTool(
                    "my_account",
                    "The player's coins and gathered resources. These belong to the account and are " +
                    "shared by every character, so there is one figure, never one per character.",
                    AiTool.NoParameters,
                    (args, ct) => Task.FromResult(Account(userId))),

                new AiTool(
                    "character_skills",
                    "Every skill level for one of the player's characters, with how far through the " +
                    "current level each one is. Use this for any question about a level.",
                    Schema("character", "The character's name or number, as given by my_characters."),
                    (args, ct) => Task.FromResult(Skills(userId, Text(args, "character")))),

                new AiTool(
                    "where_is_character",
                    "Where one of the player's characters is right now and what it is doing: island, " +
                    "task, health, whether it is in a raid, dungeon, arena or onsen, and how much " +
                    "experience an hour it is earning.",
                    Schema("character", "The character's name or number, as given by my_characters."),
                    (args, ct) => Task.FromResult(Whereabouts(userId, Text(args, "character")))),

                new AiTool(
                    "character_equipment",
                    "What one of the player's characters is wearing and wielding, and which pet is " +
                    "out. Use this for anything about equipment, and before suggesting a change.",
                    Schema("character", "The character's name or number, as given by my_characters."),
                    (args, ct) => Task.FromResult(Equipment(userId, Text(args, "character")))),

                new AiTool(
                    "search_items",
                    "Search every item in the game by name, with its stats, level requirements and " +
                    "what the vendor pays. Use this to answer what an item is or to compare items, " +
                    "not just what the player already owns.",
                    Schema("search", "Part of an item's name."),
                    (args, ct) => Task.FromResult(SearchItems(Text(args, "search")))),

                new AiTool(
                    "character_items",
                    "What one of the player's characters is carrying. Names, amounts, whether each " +
                    "is equipped, and whether it is soulbound.",
                    Schema("character", "The character's name or number, as given by my_characters."),
                    (args, ct) => Task.FromResult(Inventory(userId, Text(args, "character")))),

                // The stash is the user's, not any one character's, which is why it is a tool of
                // its own rather than a corner of character_items.
                new AiTool(
                    "stash_items",
                    "The player's item stash: everything they own that no character is carrying. " +
                    "Optionally filtered to names containing a word.",
                    Schema("search", "A word to filter item names by, or null for everything.", nullable: true),
                    (args, ct) => Task.FromResult(Stash(userId, Text(args, "search")))),

                new AiTool(
                    "vendor_stock",
                    "What the vendor has for sale, with the price to buy one and how many are left. " +
                    "Optionally filtered to names containing a word.",
                    Schema("search", "A word to filter item names by, or null for everything.", nullable: true),
                    (args, ct) => Task.FromResult(Vendor(Text(args, "search")))),

                // Whether the bot is in this person's channel. The global count was all it could
                // see, so asked the one question that matters it could only say it did not know.
                new AiTool(
                    "my_bot_status",
                    "Whether the Ravenfall chat bot is in this person's own channel, which channel " +
                    "it joins, whether their Twitch name and their Ravenfall account name still " +
                    "agree, and whether their game is running. Use this for any question about the " +
                    "bot being connected, joined, or not responding to commands.",
                    AiTool.NoParameters,
                    (args, ct) => Task.FromResult(BotStatus(userId))),

                new AiTool(
                    "my_town",
                    "The player's town: its level, how far off the next one is, its resources, and " +
                    "every plot with what is built on it and who lives there. Call this before " +
                    "changing anything about the town.",
                    AiTool.NoParameters,
                    (args, ct) => Task.FromResult(Town(userId))),

                // Where things are on the site. Answering "you can do that on your stash page" is
                // a worse answer than the same sentence with a button on it.
                new AiTool(
                    "list_pages",
                    "The pages of the site and what each one is for. Use this when somebody asks " +
                    "where to do something, or asks to be taken somewhere.",
                    AiTool.NoParameters,
                    (args, ct) => Task.FromResult(ListPages(isAdministrator))),

                new AiTool(
                    "offer_character_link",
                    "Put a button under your answer that opens one of the player's own characters, " +
                    "optionally straight onto a tab. Use this rather than offer_link whenever the " +
                    "answer is about a particular character.",
                    CharacterLinkSchema,
                    (args, ct) => Task.FromResult(OfferCharacterLink(offers, userId, args))),

                new AiTool(
                    "offer_link",
                    "Put a button under your answer that takes the person to a page. Use it whenever " +
                    "your answer mentions somewhere on the site, and when they ask to be taken " +
                    "somewhere. Say what they will find there; the button does the going.",
                    LinkSchema,
                    (args, ct) => Task.FromResult(OfferLink(offers, isAdministrator, args))),

                // What the assistant knows that is not in the game's data: rules, mechanics,
                // and anything an administrator has written down.
                new AiTool(
                    "search_knowledge",
                    "Look up how something in Ravenfall works: rules, mechanics, costs, and anything " +
                    "written down about the game. Use this before saying you do not know, and before " +
                    "explaining any mechanic from memory.",
                    Schema("question", "What to look up, in the player's own words."),
                    (args, ct) => Task.FromResult(SearchKnowledge(Text(args, "question")))),

                // Writing one down. What this does depends on who is asking, which is the point.
                new AiTool(
                    "remember_correction",
                    "Record a correction when the person tells you something you said was wrong, or " +
                    "teaches you something about the game worth keeping. Say plainly afterwards " +
                    "whether it has been saved or sent for review.",
                    CorrectionSchema,
                    (args, ct) => Task.FromResult(RememberCorrection(userId, isAdministrator, isModerator, args))),

                // Telling the running game to do something. Same calls the Twitch overlay makes,
                // so this is the extension's buttons reachable from a conversation rather than a
                // new power.
                new AiTool(
                    "control_character",
                    "Tell the running game to do something with one of the player's characters: " +
                    "change what it is training, sail to an island, rest in the onsen or stop, or " +
                    "join a raid or dungeon. Only works while the character is in a live stream. " +
                    "The player has to agree before it happens.",
                    ControlSchema,
                    (args, ct) => Task.FromResult(Control(userId, args)),
                    requiresConfirmation: true,
                    summarise: args => DescribeControl(userId, args)),

                // The town writes. Same three calls the town page's controls make, so this is
                // those controls reachable from a sentence rather than a second way to edit a town.
                new AiTool(
                    "set_all_plots",
                    "Build every plot in the player's town as one house type and move the best " +
                    "people on their stream into them, which is what the in game quick command " +
                    "does. Replaces every current assignment, so it has to be agreed to first.",
                    Schema("type", "The house type, for instance Melee, Magic, Ranged, Mining or Woodcutting."),
                    (args, ct) => SetAllPlots(userId, Text(args, "type")),
                    requiresConfirmation: true,
                    summarise: args => DescribeSetAllPlots(userId, Text(args, "type"))),

                new AiTool(
                    "set_plot_type",
                    "Change what is built on one plot of the player's town, keeping whoever lives " +
                    "there. Has to be agreed to first.",
                    PlotTypeSchema,
                    (args, ct) => SetPlotType(userId, args),
                    requiresConfirmation: true,
                    summarise: args => DescribeSetPlotType(userId, args)),

                new AiTool(
                    "set_plot_occupant",
                    "Put somebody playing on the stream into one plot of the player's town, or " +
                    "empty it. A person can only live on one plot, so this moves them off any " +
                    "other. Has to be agreed to first.",
                    PlotOccupantSchema,
                    (args, ct) => SetPlotOccupant(userId, args),
                    requiresConfirmation: true,
                    summarise: args => DescribeSetPlotOccupant(userId, args)),

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

            if (!isAdministrator) return tools;

            // Added rather than swapped in. An administrator is still a player with characters of
            // their own, and asks about them like anybody else.
            tools.Add(new AiTool(
                "server_status",
                "How the server is doing right now: how many characters are in game, how many streams " +
                "are live, whether the bot is responding, and any active experience multiplier.",
                AiTool.NoParameters,
                (args, ct) => Task.FromResult(ServerStatus())));

            tools.Add(new AiTool(
                "live_streams",
                "The streams running right now, busiest first, with how many players are in each and " +
                "how long each has been going.",
                AiTool.NoParameters,
                (args, ct) => Task.FromResult(LiveStreams())));

            tools.Add(new AiTool(
                "unstuck_character",
                "Free a character the server still believes is in a session, when it is stuck and " +
                "cannot rejoin. Same thing as the Unstuck button on a character's skills page. " +
                "Has to be agreed to first.",
                Schema("character", "The character's name or number."),
                (args, ct) => Unstuck(userId, Text(args, "character")),
                requiresConfirmation: true,
                summarise: args => "Unstuck " + (Text(args, "character") ?? "a character") +
                                   ", which frees it from the session it is stuck in."));

            tools.Add(new AiTool(
                "find_player",
                "Look up any player by name and see their characters, levels and coins. " +
                "Administrators only.",
                Schema("name", "The player's user name."),
                (args, ct) => Task.FromResult(FindPlayer(Text(args, "name")))));

            return tools;
        }

        /// <summary>
        ///     Where a character is and what it is doing.
        /// </summary>
        /// <remarks>
        ///     State only exists while a character is in a running stream, so "nothing to report" is
        ///     a real answer here rather than a failure, and it is said plainly. A character that is
        ///     not playing is not lost.
        /// </remarks>
        private string Whereabouts(Guid userId, string which)
        {
            var character = Resolve(userId, which);
            if (character == null) return NoSuchCharacter(userId, which);

            var state = character.State;
            if (state == null)
            {
                return character.Name + " is not in a running stream at the moment, so there is " +
                       "nothing to report about where it is.";
            }

            // Whose stream the character is actually in. This was not reported at all, so asked
            // what stream they were in the assistant said it could not see, which was wrong: the
            // character knows, through the session it is locked to.
            var session = gameData.GetSessionByCharacterId(character.Id);
            var streamer = session == null ? null : gameData.GetUser(session.UserId)?.UserName;

            return Json(new
            {
                character = character.Name,
                doing = Doing(character),
                inTheStreamOf = streamer,
                island = string.IsNullOrWhiteSpace(state.Island) ? null : state.Island,
                sailingTo = string.IsNullOrWhiteSpace(state.Destination) ? null : state.Destination,
                health = state.Health,
                inRaid = state.InRaid,
                inDungeon = state.InDungeon,
                inArena = state.InArena,
                restingInOnsen = state.InOnsen,
                experiencePerHour = state.ExpPerHour
            });
        }

        /// <summary>
        ///     What is worn and wielded, and which pet is out.
        /// </summary>
        private string Equipment(Guid userId, string which)
        {
            var character = Resolve(userId, which);
            if (character == null) return NoSuchCharacter(userId, which);

            var worn = (character.InventoryItems ?? Array.Empty<RavenNest.Models.InventoryItem>())
                .Where(x => x.Equipped)
                .Select(x => new
                {
                    name = ItemName(x),
                    slot = gameData.GetItem(x.ItemId)?.Type.ToString(),
                    enchanted = !string.IsNullOrEmpty(x.Enchantment),
                    enchantment = x.Enchantment
                })
                .ToList();

            var pet = character.ActiveBattlePet == null
                ? null
                : (character.BattlePets ?? Array.Empty<RavenNest.Models.BattlePet>())
                    .FirstOrDefault(x => x.Id == character.ActiveBattlePet.Value);

            return Json(new
            {
                character = character.Name,
                combatLevel = character.CombatLevel,
                equipped = worn,
                nothingEquipped = worn.Count == 0,
                pet = pet == null
                    ? null
                    : new { name = pet.Name, type = pet.Type.ToString(), tier = pet.Tier.ToString() },
                petsOwned = character.BattlePets?.Count ?? 0
            });
        }

        /// <summary>
        ///     The item catalogue, so questions can be about items the player does not own.
        /// </summary>
        /// <remarks>
        ///     Capped, because the catalogue is thousands of items and a broad word would otherwise
        ///     return a list nobody can read and every one of them is tokens. The reply says when it
        ///     has been cut rather than quietly presenting the first twenty as the whole answer.
        /// </remarks>
        private string SearchItems(string search)
        {
            if (string.IsNullOrWhiteSpace(search)) return "Give a word to search for.";

            var matches = gameData.GetItems()
                .Where(x => x.Name != null &&
                            x.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(x => x.Name.Length)
                .ToList();

            if (matches.Count == 0) return "No item matches '" + search + "'.";

            const int limit = 20;
            var shown = matches.Take(limit).Select(x => new
            {
                name = x.Name,
                type = x.Type.ToString(),
                material = x.Material.ToString(),
                weaponAim = x.WeaponAim,
                weaponPower = x.WeaponPower,
                magicAim = x.MagicAim,
                magicPower = x.MagicPower,
                rangedAim = x.RangedAim,
                rangedPower = x.RangedPower,
                armourPower = x.ArmorPower,
                requires = new
                {
                    attack = x.RequiredAttackLevel,
                    defense = x.RequiredDefenseLevel,
                    magic = x.RequiredMagicLevel,
                    ranged = x.RequiredRangedLevel,
                    slayer = x.RequiredSlayerLevel
                },
                vendorPays = x.ShopSellPrice
            });

            return Json(new
            {
                matched = matches.Count,
                showing = Math.Min(limit, matches.Count),
                note = matches.Count > limit
                    ? "Only the closest " + limit + " are shown. Ask with a more specific word for the rest."
                    : null,
                items = shown
            });
        }

        /// <summary>
        ///     Frees a stuck character, through the same service call the page's button uses.
        /// </summary>
        /// <remarks>
        ///     An administrator tool, because the button is behind an administrator check on the
        ///     skills page and the chat is not meant to be a wider door than the page. The service
        ///     underneath now also refuses a character that is neither yours nor yours to
        ///     administer, which it did not before.
        /// </remarks>
        private async Task<string> Unstuck(Guid userId, string which)
        {
            var character = Resolve(userId, which);
            var characterId = character?.Id ?? Guid.Empty;

            if (characterId == Guid.Empty)
            {
                // An administrator may well mean somebody else's character, so fall back to a
                // search by name rather than only their own list.
                var found = gameData.FindCharacter(x =>
                    x.Name != null && x.Name.Equals(which, StringComparison.OrdinalIgnoreCase));

                if (found == null) return "There is no character called '" + which + "'.";
                characterId = found.Id;
            }

            var ok = await playerService.UnstuckPlayerAsync(characterId);
            return ok
                ? "Done. It should be able to rejoin now."
                : "That did not work. It may not have been stuck, or it may not be one you can free.";
        }

        // ---- the bot, and the town ---------------------------------------------------------------

        /// <summary>
        ///     Whether the bot is in this person's channel.
        /// </summary>
        /// <remarks>
        ///     Only server_status existed, which reports how many channels the bot is in across
        ///     everybody. Asked "is it in mine" the honest answer from that number is no idea, which
        ///     is what it kept saying. BotService has known the answer all along, for the banner at
        ///     the top of the bot page; it just was not reachable from here.
        /// </remarks>
        private string BotStatus(Guid userId)
        {
            var status = botService.GetBotStatusFor(userId);

            return Json(new
            {
                botOnline = status.BotOnline,
                inYourChannel = status.BotInChannel,
                theChannelItJoins = status.ExpectedChannel,
                yourTwitchLogin = status.TwitchLogin,
                // Worth naming rather than leaving to be inferred from the two names differing: a
                // rename is the usual reason the bot is up, in a channel, and still silent.
                nameMismatch = status.NameMismatch,
                yourGameIsRunning = status.HasActiveGameSession,
                botUptime = status.BotOnline ? Describe(status.BotUptime) : null
            });
        }

        private string Town(Guid userId)
        {
            var town = townService.GetMyTownAsync(userId).GetAwaiter().GetResult();
            if (town == null) return "You do not have a town.";

            return Json(new
            {
                name = town.Name,
                level = town.Level,
                experienceToNextLevel = Math.Round(town.ExperienceToNextLevel),
                timeToNextLevel = town.TimeToNextLevel == null ? null : Describe(town.TimeToNextLevel.Value),
                // Which decides what a change to the town can do: with the game off there is nobody
                // playing to move into a plot.
                yourGameIsRunning = town.IsStreamLive,
                plots = town.TotalSlotCount,
                plotsWithSomethingBuilt = town.BuiltSlotCount,
                plotsEarningBonus = town.ActiveSlotCount,
                nextPlotAtTownLevel = town.NextSlotAtTownLevel,
                resources = new
                {
                    coins = Math.Floor(town.Coins),
                    wood = Math.Floor(town.Wood),
                    ore = Math.Floor(town.Ore),
                    fish = Math.Floor(town.Fish),
                    wheat = Math.Floor(town.Wheat)
                },
                // Numbered from one, the same as the page and the same as the messages the town
                // service writes, so a plot referred to in conversation is the plot on screen.
                plotList = town.Houses.Select(h => new
                {
                    plot = h.Slot + 1,
                    built = h.IsBuilt ? TownHouseTypes.LabelOf(h.Type) : null,
                    livesHere = h.AssignedCharacterName,
                    skillLevel = h.IsAssigned ? h.SkillLevel : (int?)null,
                    bonus = h.IsAssigned ? Math.Round(h.Bonus, 1) : (double?)null,
                    earningItNow = h.IsActive
                }).ToArray()
            });
        }

        /// <summary>
        ///     Reads a house type out of whatever the person called it.
        /// </summary>
        /// <remarks>
        ///     Matched against the labels rather than parsed off the enum, because the enum holds
        ///     two values that are not house types, and because a Melee house is driven by Health
        ///     and somebody may well ask for it by that name.
        /// </remarks>
        private static TownHouseSlotType? ParseHouseType(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;

            var wanted = text.Trim();
            foreach (var option in TownHouseTypes.All)
            {
                if (option.Label.Equals(wanted, StringComparison.OrdinalIgnoreCase) ||
                    option.SkillName.Equals(wanted, StringComparison.OrdinalIgnoreCase))
                {
                    return option.Type;
                }
            }

            return null;
        }

        private static string NoSuchHouseType(string text)
        {
            return "There is no '" + text + "' house. It can be one of: " +
                   string.Join(", ", TownHouseTypes.All.Select(x => x.Label)) + ".";
        }

        private async Task<string> SetAllPlots(Guid userId, string type)
        {
            var parsed = ParseHouseType(type);
            if (parsed == null) return NoSuchHouseType(type);

            var result = await townService.SetAllHousesAsync(userId, parsed.Value);
            return result.Message;
        }

        /// <summary>
        ///     What setting every plot would do, for the person to agree to or not.
        /// </summary>
        /// <remarks>
        ///     Built from the same plan the write uses, so the two cannot describe different things.
        ///     Worth the extra pass: this replaces every assignment in the town at once, and it is
        ///     the one thing the in game command cannot show before it commits.
        /// </remarks>
        private string DescribeSetAllPlots(Guid userId, string type)
        {
            var parsed = ParseHouseType(type);
            if (parsed == null) return "Set every plot to " + type + ", which is not a house type.";

            var label = TownHouseTypes.LabelOf(parsed.Value);
            var plan = townService.PlanSetAllHousesAsync(userId, parsed.Value).GetAwaiter().GetResult();
            if (plan == null) return "Set every plot in your town to " + label + ".";

            if (!plan.IsStreamLive)
            {
                return "Build all " + plan.PlotCount + " plots as " + label + " houses. Nobody moves, " +
                       "because with your game off there is nobody playing to move in.";
            }

            if (plan.Picks.Count == 0)
            {
                return "Build all " + plan.PlotCount + " plots as " + label + " houses and empty " +
                       "every one of them, because nobody is playing on your stream right now.";
            }

            var names = string.Join(", ", plan.Picks.Take(5).Select(x => x.Name));
            if (plan.Picks.Count > 5) names += " and " + (plan.Picks.Count - 5) + " more";

            var summary = "Build all " + plan.PlotCount + " plots as " + label + " houses and move in " +
                          names + ", for " + Math.Round(plan.TotalBonus) + "% bonus.";

            if (plan.EmptyPlotCount > 0)
            {
                summary += " " + plan.EmptyPlotCount + " " +
                           (plan.EmptyPlotCount == 1 ? "plot is" : "plots are") +
                           " left empty, and anyone currently in them is moved out.";
            }
            else
            {
                summary += " Everyone currently living in a plot who is not on that list is moved out.";
            }

            return summary;
        }

        private async Task<string> SetPlotType(Guid userId, JsonElement args)
        {
            var plot = (int)Number(args, "plot");
            var type = Text(args, "type");

            // Demolishing is the one case with no house type behind it, and it also empties the
            // plot, which is why the town service treats Undefined as its own thing.
            TownHouseSlotType wanted;
            if (IsClearing(type))
            {
                wanted = TownHouseSlotType.Undefined;
            }
            else
            {
                var parsed = ParseHouseType(type);
                if (parsed == null) return NoSuchHouseType(type);
                wanted = parsed.Value;
            }

            var result = await townService.SetHouseTypeAsync(userId, plot - 1, wanted);
            return result.Message;
        }

        private string DescribeSetPlotType(Guid userId, JsonElement args)
        {
            var plot = (int)Number(args, "plot");
            var type = Text(args, "type");

            if (IsClearing(type)) return "Demolish whatever is on plot " + plot + ", emptying it.";

            var parsed = ParseHouseType(type);
            if (parsed == null) return "Build plot " + plot + " as a " + type + " house, which is not a house type.";

            return "Build plot " + plot + " as a " + TownHouseTypes.LabelOf(parsed.Value) +
                   " house, keeping whoever lives there.";
        }

        private static bool IsClearing(string type)
        {
            if (string.IsNullOrWhiteSpace(type)) return false;

            var t = type.Trim();
            return t.Equals("none", StringComparison.OrdinalIgnoreCase) ||
                   t.Equals("empty", StringComparison.OrdinalIgnoreCase) ||
                   t.Equals("clear", StringComparison.OrdinalIgnoreCase) ||
                   t.Equals("demolish", StringComparison.OrdinalIgnoreCase);
        }

        private async Task<string> SetPlotOccupant(Guid userId, JsonElement args)
        {
            var plot = (int)Number(args, "plot");
            var who = Text(args, "who");

            if (string.IsNullOrWhiteSpace(who))
            {
                var emptied = await townService.SetHouseOccupantAsync(userId, plot - 1, null);
                return emptied.Message;
            }

            var candidate = FindCandidate(userId, plot, who);
            if (candidate == null) return NoSuchCandidate(userId, plot, who);

            var result = await townService.SetHouseOccupantAsync(userId, plot - 1, candidate.CharacterId);
            return result.Message;
        }

        private string DescribeSetPlotOccupant(Guid userId, JsonElement args)
        {
            var plot = (int)Number(args, "plot");
            var who = Text(args, "who");

            if (string.IsNullOrWhiteSpace(who)) return "Empty plot " + plot + ", so anyone on your stream can claim it.";

            var candidate = FindCandidate(userId, plot, who);
            if (candidate == null) return "Move " + who + " into plot " + plot + ".";

            var summary = "Move " + candidate.Name + " into plot " + plot +
                          ", worth " + Math.Round(candidate.Bonus) + "% bonus.";

            if (candidate.CurrentSlot != null)
            {
                summary += " They are on plot " + (candidate.CurrentSlot.Value + 1) +
                           " at the moment, which this empties.";
            }

            return summary;
        }

        /// <summary>
        ///     Resolves a name against the people who could actually take this plot.
        /// </summary>
        /// <remarks>
        ///     Deliberately narrower than a search of every player. Only somebody playing on this
        ///     stream can hold a plot usefully, and the town service refuses anybody else anyway, so
        ///     resolving against a wider list would only produce a better looking way to fail.
        /// </remarks>
        private TownCandidate FindCandidate(Guid userId, int plot, string who)
        {
            var candidates = townService.GetHouseCandidatesAsync(userId, plot - 1).GetAwaiter().GetResult();
            if (candidates == null || candidates.Count == 0) return null;

            var wanted = (who ?? "").Trim();

            return candidates.FirstOrDefault(x => string.Equals(x.Name, wanted, StringComparison.OrdinalIgnoreCase))
                ?? candidates.FirstOrDefault(x => string.Equals(x.UserName, wanted, StringComparison.OrdinalIgnoreCase))
                ?? candidates.FirstOrDefault(x =>
                       x.Name != null && x.Name.StartsWith(wanted, StringComparison.OrdinalIgnoreCase));
        }

        private string NoSuchCandidate(Guid userId, int plot, string who)
        {
            var candidates = townService.GetHouseCandidatesAsync(userId, plot - 1).GetAwaiter().GetResult();
            if (candidates == null || candidates.Count == 0)
            {
                return "Nobody can move into plot " + plot + " right now. Either nothing is built on " +
                       "it, or nobody is playing on your stream.";
            }

            return "There is nobody called '" + who + "' playing on your stream. These can take plot " +
                   plot + ": " + string.Join(", ", candidates.Take(10).Select(x => x.Name)) + ".";
        }

        // ---- telling the game to do something ---------------------------------------------------

        /// <summary>
        ///     Carries out a control action, once the player has agreed to it.
        /// </summary>
        /// <remarks>
        ///     Everything here goes through GameActions, which resolves the character against the
        ///     signed in user and finds the session it is actually in. Nothing is taken on trust
        ///     from the arguments except which character was meant, and that is resolved by name
        ///     against this user's own list.
        /// </remarks>
        private string Control(Guid userId, JsonElement args)
        {
            var character = Resolve(userId, Text(args, "character"));
            if (character == null) return NoSuchCharacter(userId, Text(args, "character"));

            if (!TryReadAction(Text(args, "action"), out var kind))
            {
                return "There is no action called '" + Text(args, "action") + "'.";
            }

            var result = gameActions.Run(character.Id, userId, kind, Text(args, "detail"));
            return result.Message;
        }

        private string DescribeControl(Guid userId, JsonElement args)
        {
            var character = Resolve(userId, Text(args, "character"));
            if (character == null || !TryReadAction(Text(args, "action"), out var kind))
            {
                return "Do something with your character.";
            }

            return gameActions.Describe(character.Id, kind, Text(args, "detail"));
        }

        private static bool TryReadAction(string text, out GameActionKind kind)
        {
            kind = default;
            if (string.IsNullOrWhiteSpace(text)) return false;

            switch (text.Trim().ToLowerInvariant())
            {
                case "train": kind = GameActionKind.Train; return true;
                case "travel": kind = GameActionKind.Travel; return true;
                case "rest": kind = GameActionKind.Rest; return true;
                case "stop_resting": kind = GameActionKind.StopResting; return true;
                case "join_raid": kind = GameActionKind.JoinRaid; return true;
                case "join_dungeon": kind = GameActionKind.JoinDungeon; return true;
                default: return false;
            }
        }

        private static readonly string ControlSchema =
            "{\"type\":\"object\",\"properties\":{" +
            "\"character\":{\"type\":\"string\",\"description\":\"Which character, by name or number.\"}," +
            "\"action\":{\"type\":\"string\",\"enum\":[\"train\",\"travel\",\"rest\",\"stop_resting\",\"join_raid\",\"join_dungeon\"]," +
            "\"description\":\"What to do.\"}," +
            "\"detail\":{\"type\":[\"string\",\"null\"],\"description\":\"For train, the skill such as magic or mining. " +
            "For travel, the island: " + string.Join(", ", GameActions.IslandNames) + ". Null otherwise.\"}}," +
            "\"required\":[\"character\",\"action\",\"detail\"],\"additionalProperties\":false}";

        // ---- getting around --------------------------------------------------------------------

        private static string ListPages(bool isAdministrator)
        {
            return Json(SitePages.For(isAdministrator).Select(x => new
            {
                page = x.Path,
                name = x.Name,
                what = x.What
            }));
        }

        /// <summary>
        ///     Adds a button to the answer being written.
        /// </summary>
        /// <remarks>
        ///     The page has to be one of ours, resolved from the list rather than taken as given, so
        ///     the assistant cannot send anybody to an address it invented or to an admin page they
        ///     cannot open. A button that leads nowhere is worse than a sentence telling them where
        ///     to look.
        ///
        ///     <para>
        ///     It offers rather than navigates. Moving somebody's browser out from under them while
        ///     they are still reading the answer is startling, and one click is not the part that
        ///     was hard. If it should jump straight there, that is a change in the widget rather
        ///     than here.
        ///     </para>
        /// </remarks>
        private static string OfferLink(IList<AiOffer> offers, bool isAdministrator, JsonElement args)
        {
            var wanted = Text(args, "page");
            var page = SitePages.Resolve(wanted, isAdministrator);

            if (page == null)
            {
                return "There is no page called '" + wanted + "'. Call list_pages and use one of those " +
                       "paths, and do not invent an address.";
            }

            // Their words for it when they gave some, since "Open your clan stash" reads better than
            // whatever the page is called in the menu.
            var label = Text(args, "label");
            if (string.IsNullOrWhiteSpace(label)) label = "Open " + page.Name;

            if (offers.Any(x => x.Target == page.Path)) return "That button is already there.";

            offers.Add(new AiOffer(AiOfferKind.Navigate, label.Trim(), page.Path));
            return "A button to " + page.Name + " has been added under your answer. Mention what they " +
                   "will find there rather than describing the button.";
        }

        /// <summary>
        ///     A button that opens one of this user's characters, on a named tab.
        /// </summary>
        /// <remarks>
        ///     The general page link could only reach /characters, which lands on whichever
        ///     character was last open, on its overview. Asked to open a character's inventory it
        ///     produced a button that went almost nowhere, which reads as broken rather than as
        ///     approximate.
        ///
        ///     <para>
        ///     The character is resolved against this user's own list, so the address is built out
        ///     of something they own rather than out of anything the model supplied.
        ///     </para>
        /// </remarks>
        private string OfferCharacterLink(IList<AiOffer> offers, Guid userId, JsonElement args)
        {
            var which = Text(args, "character");
            var character = Resolve(userId, which);
            if (character == null) return NoSuchCharacter(userId, which);

            var tab = (Text(args, "tab") ?? "").Trim().ToLowerInvariant();
            if (tab.Length > 0 && !KnownTabs.Contains(tab))
            {
                return "There is no tab called '" + tab + "'. It can be one of: " +
                       string.Join(", ", KnownTabs) + ".";
            }

            var target = "/characters/" + character.CharacterIndex;
            if (tab.Length > 0) target += "/" + tab;

            var label = Text(args, "label");
            if (string.IsNullOrWhiteSpace(label))
            {
                label = tab.Length > 0
                    ? "Open " + character.Name + " " + tab
                    : "Open " + character.Name;
            }

            if (offers.Any(x => x.Target == target)) return "That button is already there.";

            offers.Add(new AiOffer(AiOfferKind.Navigate, label.Trim(), target));
            return "A button to " + character.Name + (tab.Length > 0 ? " " + tab : "") +
                   " has been added under your answer.";
        }

        private static readonly string[] KnownTabs = { "overview", "skills", "inventory", "clan" };

        private static readonly string CharacterLinkSchema =
            "{\"type\":\"object\",\"properties\":{" +
            "\"character\":{\"type\":\"string\",\"description\":\"Which character, by name or number.\"}," +
            "\"tab\":{\"type\":[\"string\",\"null\"],\"description\":\"One of overview, skills, inventory, clan, or null for the overview.\"}," +
            "\"label\":{\"type\":[\"string\",\"null\"],\"description\":\"What the button says, or null for a default.\"}}," +
            "\"required\":[\"character\",\"tab\",\"label\"],\"additionalProperties\":false}";

        private const string LinkSchema =
            "{\"type\":\"object\",\"properties\":{" +
            "\"page\":{\"type\":\"string\",\"description\":\"The path from list_pages, such as /stash.\"}," +
            "\"label\":{\"type\":[\"string\",\"null\"],\"description\":\"What the button should say, or null for a default.\"}}," +
            "\"required\":[\"page\",\"label\"],\"additionalProperties\":false}";

        // ---- knowledge -------------------------------------------------------------------------

        /// <summary>
        ///     What the knowledge base has on a question.
        /// </summary>
        /// <remarks>
        ///     Returning nothing is a useful answer rather than a failure, and it is recorded: the
        ///     questions that find nothing are the only honest list of what is worth writing down.
        ///     The reply says so plainly, because an assistant that pads out a gap with plausible
        ///     reasoning is how a wrong mechanic ends up being repeated.
        /// </remarks>
        private string SearchKnowledge(string question)
        {
            var found = facts.Search(question);

            if (found.Count == 0)
            {
                return "Nothing is written down about that. Say you do not know rather than working " +
                       "it out, offer to remember a correction if they can tell you, and do not guess.";
            }

            return Json(found.Select(x => new
            {
                title = x.Title,
                answer = x.Body,
                source = x.Source.ToString(),
                link = x.SourceUrl,
                note = x.Status == FactStatus.Stale
                    ? "This may be out of date: something in the game it describes has changed."
                    : null
            }));
        }

        /// <summary>
        ///     Writes down a correction, at the authority of whoever gave it.
        /// </summary>
        /// <remarks>
        ///     An administrator's or a moderator's correction is accepted immediately, because they
        ///     already have that authority through the site and the chat is not a second, weaker set
        ///     of rules. Everybody else's is proposed and waits for review.
        ///
        ///     <para>
        ///     That difference is decided here from the signed in session, never from anything the
        ///     model was told. A conversation cannot argue its way into publishing.
        ///     </para>
        ///
        ///     <para>
        ///     It does not overwrite anything. A correction that replaces an existing fact records
        ///     which one, and the old fact is only retired when the new one is accepted, so nothing
        ///     is lost while a proposal is still a proposal.
        ///     </para>
        /// </remarks>
        private string RememberCorrection(Guid userId, bool isAdministrator, bool isModerator, JsonElement args)
        {
            var title = Text(args, "title");
            var body = Text(args, "correction");

            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(body))
            {
                return "A correction needs both a short title and the correction itself.";
            }

            var user = gameData.GetUser(userId);
            var trusted = isAdministrator || isModerator;

            var replaces = Text(args, "replaces");
            Guid? supersedes = null;
            if (!string.IsNullOrWhiteSpace(replaces))
            {
                var existing = facts.Search(replaces, 1).FirstOrDefault();
                if (existing != null && existing.IsEditable) supersedes = existing.Id;
            }

            var fact = facts.Save(new Fact
            {
                Title = title,
                Body = body,
                Source = FactSource.Learned,
                Status = trusted ? FactStatus.Published : FactStatus.Proposed,
                CreatedBy = user?.UserName ?? "a player",
                AcceptedBy = trusted ? user?.UserName : null,
                AcceptedUtc = trusted ? DateTime.UtcNow : (DateTime?)null,
                Supersedes = supersedes,
                Context = Text(args, "context")
            });

            if (trusted && supersedes != null)
            {
                facts.Accept(fact.Id, user?.UserName);
            }

            return trusted
                ? "Saved, and it will be used from now on."
                : "Written down and sent to an administrator to check. It will not be used until " +
                  "somebody approves it. Tell the person exactly that; do not imply you have learned it.";
        }

        private const string CorrectionSchema =
            "{\"type\":\"object\",\"properties\":{" +
            "\"title\":{\"type\":\"string\",\"description\":\"The question this answers, in a line.\"}," +
            "\"correction\":{\"type\":\"string\",\"description\":\"What is actually true, in a sentence or two.\"}," +
            "\"replaces\":{\"type\":[\"string\",\"null\"],\"description\":\"The title of an existing fact this corrects, or null.\"}," +
            "\"context\":{\"type\":[\"string\",\"null\"],\"description\":\"What was being discussed, so a reviewer can judge it.\"}}," +
            "\"required\":[\"title\",\"correction\",\"replaces\",\"context\"],\"additionalProperties\":false}";

        // ---- administrator tools ---------------------------------------------------------------

        /// <summary>
        ///     Read through the same ServerService call the admin overview page renders.
        /// </summary>
        /// <remarks>
        ///     Deliberately the same call rather than a second one that counts sessions itself. Two
        ///     sources for one number is how the assistant ends up contradicting the page it is
        ///     floating on top of, and the reader has no way to tell which one is wrong.
        /// </remarks>
        private string ServerStatus()
        {
            var overview = serverService.GetServerOverview();
            var multiplier = overview.Multiplier;

            return Json(new
            {
                playersInGame = overview.PlayersInGame,
                liveStreams = overview.StreamCount,
                busiestStream = overview.TopStream == null
                    ? null
                    : overview.TopStream.UserName + " with " + overview.TopStream.PlayerCount,
                botOnline = overview.BotOnline,
                botChannels = overview.BotChannelCount,
                botLastHeardFromUtc = overview.BotLastUpdate == default
                    ? null
                    : overview.BotLastUpdate.ToString("u"),
                experienceMultiplier = multiplier == null || multiplier.Multiplier <= 1
                    ? null
                    : new
                    {
                        multiplier = multiplier.Multiplier,
                        startedBy = multiplier.StartedByPlayer ? multiplier.StartedBy : "an administrator",
                        minutesLeft = (int)multiplier.Remaining.TotalMinutes
                    }
            });
        }

        private string LiveStreams()
        {
            var overview = serverService.GetServerOverview();
            if (overview.Streams.Count == 0) return "No streams are running right now.";

            var now = DateTime.UtcNow;

            return Json(overview.Streams.Select(x => new
            {
                streamer = x.UserName,
                players = x.PlayerCount,
                runningForMinutes = x.Started == default ? (int?)null : (int)(now - x.Started).TotalMinutes
            }));
        }

        private string FindPlayer(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "No name was given.";

            var user = gameData.FindUser(name.Trim());
            if (user == null) return "There is no player called '" + name + "'.";

            var players = playerManager.GetWebsitePlayers(user.Id);
            if (players == null || players.Count == 0)
            {
                return user.UserName + " exists but has no characters.";
            }

            // Coins once, for the account. Same reason as my_account: every character carries a copy
            // of the user's resources, so a figure per character is an invitation to add them up.
            var resources = gameData.GetResources(user);

            return Json(new
            {
                player = user.UserName,
                accountCoins = resources == null ? 0L : (long)resources.Coins,
                accountCoinsNote = "Shared by every character listed. Not one purse each.",
                characters = players.Select(p => new
                {
                    number = p.CharacterIndex,
                    name = p.Name,
                    combatLevel = p.CombatLevel,
                    doingNow = Doing(p)
                })
            });
        }

        // ---- what the tools return -----------------------------------------------------------

        /// <summary>
        ///     The characters, deliberately without coins.
        /// </summary>
        /// <remarks>
        ///     Coins used to be on each character here, which is how they are reached in code:
        ///     GetResources(character) looks up the character's user and returns the account's
        ///     resources. So all three characters reported the same figure, and asked how many coins
        ///     the player had, the model added them up and reported three times the real number.
        ///     It was right to trust the field; the field was lying about what it was. Coins live in
        ///     my_account now, once, where there is nothing to sum.
        /// </remarks>
        private string Characters(Guid userId)
        {
            var players = playerManager.GetWebsitePlayers(userId);
            if (players == null || players.Count == 0) return "This player has no characters.";

            var main = players.OrderBy(x => x.CharacterIndex).FirstOrDefault();

            return Json(players.Select(p => new
            {
                number = p.CharacterIndex,
                name = p.Name,
                alias = p.Alias,
                combatLevel = p.CombatLevel,
                isMain = main != null && p.Id == main.Id,
                doingNow = Doing(p)
            }));
        }

        /// <summary>
        ///     What the character is up to, in a few words, or null when nothing is known. A
        ///     character that is not in a running stream has no state to report.
        /// </summary>
        private static string Doing(WebsitePlayer player)
        {
            var state = player.State;
            if (state == null) return null;

            if (state.InDungeon) return "in a dungeon";
            if (state.InRaid) return "in a raid";
            if (state.InArena) return "in the arena";
            if (state.InOnsen) return "resting in the onsen";

            if (string.IsNullOrWhiteSpace(state.Task)) return null;

            var task = state.Task;
            if (!string.IsNullOrWhiteSpace(state.TaskArgument) &&
                !state.TaskArgument.Equals(task, StringComparison.OrdinalIgnoreCase))
            {
                task += " (" + state.TaskArgument + ")";
            }

            return string.IsNullOrWhiteSpace(state.Island) ? task : task + " on " + state.Island;
        }

        /// <summary>
        ///     Coins and resources, once, for the account.
        /// </summary>
        private string Account(Guid userId)
        {
            var user = gameData.GetUser(userId);
            var resources = gameData.GetResources(user);

            if (resources == null) return "This account has no coin purse yet.";

            return Json(new
            {
                note = "These are shared by every character on the account. Do not add them up per character.",
                coins = (long)resources.Coins,
                wood = (long)resources.Wood,
                ore = (long)resources.Ore,
                fish = (long)resources.Fish,
                wheat = (long)resources.Wheat,
                magicResource = (long)resources.Magic,
                arrows = (long)resources.Arrows
            });
        }

        /// <summary>
        ///     Every skill, from the same reflection driven list the character pages use, so a skill
        ///     added to the game turns up here without anybody remembering to add it.
        /// </summary>
        private string Skills(Guid userId, string which)
        {
            var character = Resolve(userId, which);
            if (character == null) return NoSuchCharacter(userId, which);

            if (character.Skills == null) return character.Name + " has no skills recorded.";

            var skills = character.Skills.AsList();
            if (skills.Count == 0) return character.Name + " has no skills recorded.";

            return Json(new
            {
                character = character.Name,
                combatLevel = character.CombatLevel,
                skills = skills
                    .OrderByDescending(x => x.Level)
                    .Select(x => new
                    {
                        name = x.Name,
                        level = x.Level,
                        percentIntoNextLevel = (int)Math.Round(x.Percent * 100)
                    })
            });
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

        /// <summary>
        ///     Everything in the stash, added up per item.
        /// </summary>
        /// <remarks>
        ///     The stash holds one row per deposit rather than one per item, so twenty eight boots
        ///     can be twenty eight rows. Handing that over as is invites the model to count rows and
        ///     answer twenty eight when the question was about something else, or to lose count on a
        ///     long list. Summed here, where it can be got right once.
        /// </remarks>
        private string Stash(Guid userId, string search)
        {
            var rows = gameData.GetUserBankItems(userId);
            if (rows == null || rows.Count == 0) return "The stash is empty.";

            var totals = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

            foreach (var row in rows)
            {
                if (row.Amount <= 0) continue;

                var name = string.IsNullOrWhiteSpace(row.Name)
                    ? gameData.GetItem(row.ItemId)?.Name ?? "unknown item"
                    : row.Name;

                if (!string.IsNullOrWhiteSpace(search) &&
                    name.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                totals.TryGetValue(name, out var running);
                totals[name] = running + row.Amount;
            }

            if (totals.Count == 0)
            {
                return string.IsNullOrWhiteSpace(search)
                    ? "The stash is empty."
                    : "There is nothing in the stash matching '" + search + "'.";
            }

            return Json(totals
                .OrderByDescending(x => x.Value)
                .Select(x => new { name = x.Key, amount = x.Value }));
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

        /// <summary>
        ///     A span of time in words. Rounded hard on purpose: nothing here is worth a number of
        ///     seconds, and "about 3 days" is what somebody would say back.
        /// </summary>
        private static string Describe(TimeSpan span)
        {
            if (span.TotalMinutes < 1) return "less than a minute";
            if (span.TotalHours < 1) return (int)span.TotalMinutes + " minutes";
            if (span.TotalDays < 1) return Math.Round(span.TotalHours, 1) + " hours";
            return Math.Round(span.TotalDays, 1) + " days";
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

        private const string PlotTypeSchema =
            "{\"type\":\"object\",\"properties\":{" +
            "\"plot\":{\"type\":\"integer\",\"description\":\"Which plot, numbered from one as my_town reports them.\"}," +
            "\"type\":{\"type\":\"string\",\"description\":\"The house type, for instance Melee, Magic or Mining, or 'none' to demolish it.\"}}," +
            "\"required\":[\"plot\",\"type\"],\"additionalProperties\":false}";

        private const string PlotOccupantSchema =
            "{\"type\":\"object\",\"properties\":{" +
            "\"plot\":{\"type\":\"integer\",\"description\":\"Which plot, numbered from one as my_town reports them.\"}," +
            "\"who\":{\"type\":[\"string\",\"null\"],\"description\":\"The name of somebody playing on the stream, or null to empty the plot.\"}}," +
            "\"required\":[\"plot\",\"who\"],\"additionalProperties\":false}";

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

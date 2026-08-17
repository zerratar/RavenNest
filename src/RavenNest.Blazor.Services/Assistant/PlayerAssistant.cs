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
        private readonly IServerSettingsProvider settings;

        public PlayerAssistant(
            IAiService ai,
            GameData gameData,
            PlayerManager playerManager,
            MarketPriceIndex marketPrices,
            ServerService serverService,
            FactService facts,
            IServerSettingsProvider settings)
        {
            this.ai = ai;
            this.gameData = gameData;
            this.playerManager = playerManager;
            this.marketPrices = marketPrices;
            this.serverService = serverService;
            this.facts = facts;
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
            return new AiConversation(
                ai,
                InstructionsFor(userId, isAdministrator),
                ToolsFor(userId, isAdministrator, isModerator),
                maxOutputTokens: 2000);
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
- You cannot change anything except by moving items between their own characters, and that always
  has to be agreed to first. Never say you have done something you have only proposed.
- If they ask for something you have no tool for, say what you cannot do rather than approximating
  it. Guessing which item is best without checking their skills is exactly the kind of answer that
  reads as authoritative and is not.
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

        private IReadOnlyList<AiTool> ToolsFor(Guid userId, bool isAdministrator, bool isModerator)
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
                "find_player",
                "Look up any player by name and see their characters, levels and coins. " +
                "Administrators only.",
                Schema("name", "The player's user name."),
                (args, ct) => Task.FromResult(FindPlayer(Text(args, "name")))));

            return tools;
        }

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

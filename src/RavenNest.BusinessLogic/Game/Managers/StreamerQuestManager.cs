using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Game.StreamerShop;
using RavenNest.BusinessLogic.Game.Trading;
using RavenNest.DataModels;
using RavenNest.Models;
using Character = RavenNest.DataModels.Character;
using GameSession = RavenNest.DataModels.GameSession;
using Skill = RavenNest.Models.Skill;

namespace RavenNest.BusinessLogic.Game
{
    /// <summary>
    /// Streamer quests (phase 4 of docs/quests-design.md in the game repository): quests a streamer writes for their
    /// stream in the quest window, with a name and a description of their own. A bounty pays each viewer who reaches
    /// the target, the first few of them; a stream goal is worked on by everyone together (it takes the stream quest's
    /// one place, so it never runs beside a built-in one) and its reward is shared by how much each added, the item
    /// going to the top helpers. Progress is counted as every quest is, through <see cref="QuestTracker"/>.
    /// </summary>
    /// <remarks>
    /// The reward is set aside when the quest is posted, as the tavern shelves set items aside: coins from the village
    /// treasury or one of the streamer's characters, items from the stash or a character (through
    /// <see cref="TradeExecutor"/>, all or nothing), and loyalty points from the streamer's weekly budget, which they
    /// hand out as they like (decided with the user 2026-10-25: loyalty points are one pot across all streams, so
    /// streamers may not print them freely). Whatever is left when the quest ends goes back where it came from.
    /// </remarks>
    public class StreamerQuestManager : IDisposable
    {
        public const int Bounty = 1, Goal = 2;
        public const int Running = 1, Done = 2, OutOfTime = 3, Stopped = 4, AllClaimed = 5;
        public const int MaxBounties = 3;
        public const int MaxNameLength = 40;
        public const int MaxDescriptionLength = 200;
        public const int MinMinutes = 10;
        public const int MaxMinutes = 7 * 24 * 60;
        public const int MaxBountyWinners = 100;
        public const int MaxGoalItemWinners = 20;
        public const long MaxAmount = 1_000_000;
        public const long MaxCoinsPerReward = 100_000_000;

        /// <summary>Loyalty points a streamer may hand out through quests each week. First number, set 2026-10-25.</summary>
        public const long WeeklyLoyaltyBudget = 2_500;

        private const int TopHelpersShown = 5;

        private static readonly string[] Objectives =
        {
            QuestManager.Gather, QuestManager.Kill, QuestManager.Craft, QuestManager.Cook, QuestManager.Brew,
            StreamQuestManager.Levels, QuestManager.Raid, QuestManager.Dungeon,
        };

        private static readonly Skill[] GatherSkills = { Skill.Woodcutting, Skill.Mining, Skill.Fishing, Skill.Farming, Skill.Gathering };

        private readonly GameData gameData;
        private readonly PlayerInventoryProvider inventories;
        private readonly SessionManager sessionManager;
        private readonly ILogger<StreamerQuestManager> logger;
        private readonly object mutex = new object();
        private readonly Timer timer;

        public StreamerQuestManager(GameData gameData, PlayerInventoryProvider inventories, SessionManager sessionManager,
            StreamQuestManager streamQuests, ILogger<StreamerQuestManager> logger)
        {
            this.gameData = gameData;
            this.inventories = inventories;
            this.sessionManager = sessionManager;
            this.logger = logger;
            QuestTracker.StreamerListener = this;
            streamQuests.GoalRunning = HasRunningGoal;
            this.streamQuests = streamQuests;
            timer = new Timer(_ => EndOverdue(), null, 20_000, 20_000);
        }

        private readonly StreamQuestManager streamQuests;

        // ---- What the game asks ----------------------------------------------------------------------

        /// <summary>The stream's running streamer quests, and what the streamer has to set aside for a new one.</summary>
        public StreamerQuestList GetList(SessionToken token)
        {
            var session = token != null ? gameData.GetSession(token.SessionId) : null;
            if (session == null) return null;
            var village = gameData.GetVillageBySession(session);
            var treasury = village != null ? gameData.GetResources(village.ResourcesId) : null;
            return new StreamerQuestList
            {
                Quests = gameData.GetStreamerQuests(session.UserId).Where(q => q.State == Running).OrderBy(q => q.Started).Select(Info).ToArray(),
                MaxBounties = MaxBounties,
                LoyaltyBudgetLeft = BudgetLeft(session.UserId),
                LoyaltyBudgetWeekly = WeeklyLoyaltyBudget,
                TreasuryCoins = treasury != null ? (long)treasury.Coins : 0,
                Characters = OwnCharacters(session).Select(c => new StreamerQuestCoinSource
                {
                    CharacterId = c.Id, Name = c.Name, Coins = (long)(gameData.GetResources(c)?.Coins ?? 0),
                }).ToArray(),
            };
        }

        /// <summary>The running streamer quests with one character's progress on them (!quest).</summary>
        public StreamerQuestInfo[] GetFor(SessionToken token, Guid characterId)
        {
            var character = gameData.GetCharacter(characterId);
            if (character == null || token == null || character.UserIdLock != token.UserId) return null;
            return gameData.GetStreamerQuests(token.UserId).Where(q => q.State == Running).OrderBy(q => q.Started).Select(q =>
            {
                var info = Info(q);
                var entry = gameData.GetStreamerQuestEntries(q.Id).FirstOrDefault(e => e.CharacterId == characterId);
                info.YourProgress = entry?.Progress ?? 0;
                info.YouClaimed = entry?.Completed != null;
                return info;
            }).ToArray();
        }

        /// <summary>Posts a quest: checks it, sets the reward aside, and starts it.</summary>
        public StreamerQuestResult Post(SessionToken token, StreamerQuestRequest request)
        {
            var session = token != null ? gameData.GetSession(token.SessionId) : null;
            if (session == null) return Fail("The stream could not be found.");
            if (request == null) return Fail("Nothing to post.");

            var name = Clean(request.Name, MaxNameLength);
            var description = Clean(request.Description, MaxDescriptionLength);
            var kind = request.Kind == Goal ? Goal : Bounty;
            if (!Objectives.Contains(request.Objective)) return Fail("Pick what the quest asks for.");
            var skill = request.Objective == QuestManager.Gather ? request.Skill : -1;
            if (request.Objective == QuestManager.Gather && !GatherSkills.Contains((Skill)skill)) return Fail("Pick what to gather.");
            if (request.Amount < 1 || request.Amount > MaxAmount) return Fail("The target must be between 1 and " + MaxAmount.ToString("N0", CultureInfo.InvariantCulture) + ".");
            var minutes = Math.Clamp(request.Minutes, MinMinutes, MaxMinutes);
            var winners = Math.Clamp(request.Winners, 1, kind == Bounty ? MaxBountyWinners : MaxGoalItemWinners);
            if (request.RewardCoins < 0 || request.ItemAmount < 0 || request.RewardLoyalty < 0) return Fail("A reward cannot be below zero.");
            if (request.RewardCoins > MaxCoinsPerReward) return Fail("At most " + MaxCoinsPerReward.ToString("N0", CultureInfo.InvariantCulture) + " coins a reward.");
            var hasItem = request.ItemId != null && request.ItemId != Guid.Empty && request.ItemAmount > 0;
            if (request.RewardCoins == 0 && !hasItem && request.RewardLoyalty == 0) return Fail("Give the quest a reward: coins, an item or loyalty points.");

            // a bounty sets aside one reward for each winner; a goal's coins and points are one pot, its item one per top helper
            var coins = kind == Bounty ? request.RewardCoins * winners : request.RewardCoins;
            var items = hasItem ? request.ItemAmount * winners : 0;
            var loyalty = kind == Bounty ? request.RewardLoyalty * winners : request.RewardLoyalty;

            lock (mutex)
            {
                var running = gameData.GetStreamerQuests(session.UserId).Where(q => q.State == Running).ToList();
                if (kind == Bounty && running.Count(q => q.Kind == Bounty) >= MaxBounties) return Fail("At most " + MaxBounties + " bounties at a time. Stop one first.");
                if (kind == Goal && running.Any(q => q.Kind == Goal)) return Fail("A stream goal is already running.");
                if (kind == Goal && streamQuests.IsRunning(session.UserId)) return Fail("A stream quest is already running. Stop it first, a stream has one goal at a time.");

                var week = WeekOf(DateTime.UtcNow);
                if (loyalty > BudgetLeft(session.UserId)) return Fail("That is more loyalty points than are left this week (" + BudgetLeft(session.UserId).ToString("N0", CultureInfo.InvariantCulture) + ").");

                var world = new StreamerShopWorld(gameData, inventories);
                var coinMoves = new List<CoinMove>();
                var itemMoves = new List<ItemMove>();
                Guid? coinSourceId = null;
                Village village = null;
                Character coinCharacter = null;
                if (coins > 0)
                {
                    if (request.CoinSource == 2)
                    {
                        coinCharacter = OwnCharacters(session).FirstOrDefault(c => c.Id == request.CoinCharacterId);
                        if (coinCharacter == null) return Fail("That character is not yours, or not in this stream.");
                        world.WithCharacter(coinCharacter.Id);
                        coinSourceId = coinCharacter.Id;
                    }
                    else
                    {
                        village = gameData.GetOrCreateVillageBySession(session);
                        world.WithVillage(village);
                        coinSourceId = village.Id;
                    }
                    if (world.CoinsOf(coinSourceId.Value) < coins) return Fail("There are not " + coins.ToString("N0", CultureInfo.InvariantCulture) + " coins to set aside.");
                    coinMoves.Add(new CoinMove(coinSourceId.Value, ItemMove.Outside, coins));
                }

                StackKey stack = default;
                Guid itemSource = Guid.Empty;
                if (hasItem)
                {
                    stack = new StackKey(request.ItemId.Value, request.ItemTag, request.ItemEnchantment, request.ItemTransmogrificationId, request.ItemName, request.ItemFlags);
                    if (request.ItemSourceKind == StreamerShopSourceKind.Character)
                    {
                        var character = OwnCharacters(session).FirstOrDefault(c => c.Id == request.ItemSourceCharacterId);
                        if (character == null) return Fail("That character is not yours, or not in this stream.");
                        var inventory = inventories.Get(character.Id);
                        var rows = inventory.GetUnequippedItems().Where(x => x.Key() == stack).ToList();
                        if (rows.Sum(x => x.Amount) < items) return Fail("There are not " + items.ToString("N0", CultureInfo.InvariantCulture) + " of that item to set aside.");
                        if (rows.Any(x => x.Soulbound)) return Fail("Soulbound items cannot be given away.");
                        if (rows.Any(x => inventory.IsLocked(x.Id))) return Fail("That item is busy being saved. Try again in a moment.");
                        itemSource = character.Id;
                        if (coinSourceId != character.Id) world.WithCharacter(itemSource);
                    }
                    else
                    {
                        var rows = gameData.GetUserBankItems(session.UserId).Where(x => x.Key() == stack).ToList();
                        if (rows.Sum(x => x.Amount) < items) return Fail("There are not " + items.ToString("N0", CultureInfo.InvariantCulture) + " of that item in the stash.");
                        if (rows.Any(x => x.Soulbound)) return Fail("Soulbound items cannot be given away.");
                        itemSource = session.UserId;
                        world.WithStash(itemSource);
                    }
                    itemMoves.Add(new ItemMove(itemSource, ItemMove.Outside, stack, items));
                }

                if (coinMoves.Count > 0 || itemMoves.Count > 0)
                {
                    var trade = new TradeExecutor(world).Commit(new TradePlan(TradeKind.StreamerQuestEscrow, itemMoves, coinMoves));
                    if (!trade.Ok) return Fail(trade.Reason);
                }

                if (loyalty > 0) BudgetOf(session.UserId, week).Spent += loyalty;

                var now = DateTime.UtcNow;
                var quest = new StreamerQuest
                {
                    Id = Guid.NewGuid(),
                    StreamerUserId = session.UserId,
                    Kind = kind,
                    Name = name,
                    Description = description,
                    Objective = request.Objective,
                    Skill = skill,
                    Amount = request.Amount,
                    Winners = winners,
                    State = Running,
                    Started = now,
                    Ends = now.AddMinutes(minutes),
                    RewardCoins = request.RewardCoins,
                    CoinSource = coins > 0 ? (request.CoinSource == 2 ? 2 : 1) : 0,
                    CoinSourceId = coinSourceId,
                    RewardItemId = hasItem ? stack.ItemId : (Guid?)null,
                    RewardItemTag = hasItem ? stack.Tag : null,
                    RewardItemEnchantment = hasItem ? stack.Enchantment : null,
                    RewardItemTransmogrificationId = hasItem ? stack.TransmogrificationId : null,
                    RewardItemName = hasItem ? stack.Name : null,
                    RewardItemFlags = hasItem ? stack.Flags : 0,
                    RewardItemAmount = hasItem ? request.ItemAmount : 0,
                    ItemSourceKind = (int)request.ItemSourceKind,
                    ItemSourceCharacterId = hasItem && request.ItemSourceKind == StreamerShopSourceKind.Character ? itemSource : (Guid?)null,
                    RewardLoyalty = request.RewardLoyalty,
                    LoyaltyWeek = week,
                    EscrowCoins = coins,
                    EscrowItems = items,
                    EscrowLoyalty = loyalty,
                };
                gameData.Add(quest);
                gameData.Touch();

                if (coinCharacter != null) SendCoins(coinCharacter);
                if (village != null) sessionManager.SendVillageInfo(session);
                return new StreamerQuestResult { Ok = true, Quest = Info(quest) };
            }
        }

        /// <summary>Stops a running quest; what is left of its reward goes back.</summary>
        public StreamerQuestResult Stop(SessionToken token, Guid questId)
        {
            var session = token != null ? gameData.GetSession(token.SessionId) : null;
            if (session == null) return Fail("The stream could not be found.");
            var quest = gameData.GetStreamerQuests(session.UserId).FirstOrDefault(q => q.Id == questId);
            if (quest == null || quest.State != Running) return Fail("That quest is not running.");
            End(quest, Stopped);
            return new StreamerQuestResult { Ok = true, Quest = Info(quest) };
        }

        public bool HasRunningGoal(Guid streamerUserId) =>
            gameData.GetStreamerQuests(streamerUserId).Any(q => q.Kind == Goal && q.State == Running);

        // ---- Progress -------------------------------------------------------------------------------------

        internal void Progress(Character character, string objective, int skill, long amount)
        {
            if (character?.UserIdLock == null || amount <= 0) return;
            var quests = gameData.GetStreamerQuests(character.UserIdLock.Value);
            if (quests.Count == 0) return;

            try
            {
                foreach (var quest in quests.ToList())
                {
                    if (quest.State != Running || quest.Objective != objective) continue;
                    if (quest.Skill >= 0 && skill >= 0 && quest.Skill != skill) continue;
                    if (quest.Ends <= DateTime.UtcNow) { End(quest, OutOfTime); continue; }

                    if (quest.Kind == Bounty) BountyProgress(quest, character, amount);
                    else GoalProgress(quest, character, amount);
                }
            }
            catch (Exception exc)
            {
                logger.LogError("Streamer quest progress failed for " + character.Name + ": " + exc);
            }
        }

        private void BountyProgress(StreamerQuest quest, Character character, long amount)
        {
            lock (mutex)
            {
                if (quest.State != Running) return;
                var entry = EntryOf(quest, character);
                if (entry.Completed != null) return;
                entry.Progress = Math.Min(quest.Amount, entry.Progress + amount);
                if (entry.Progress < quest.Amount || quest.Claimed >= quest.Winners) return;

                // the reward that was set aside for this winner
                entry.Completed = DateTime.UtcNow;
                quest.Claimed++;
                var coins = Math.Min(quest.RewardCoins, quest.EscrowCoins);
                var items = Math.Min(quest.RewardItemAmount, quest.EscrowItems);
                var loyalty = Math.Min(quest.RewardLoyalty, quest.EscrowLoyalty);
                Pay(quest, character, entry, coins, items, loyalty);
                gameData.Touch();

                var session = gameData.GetOwnedSessionByUserId(quest.StreamerUserId);
                if (session != null)
                {
                    gameData.EnqueueGameEvent(gameData.CreateSessionEvent(GameEventType.StreamerQuestClaimed, session,
                        new StreamerQuestClaimed { Quest = Info(quest), CharacterId = character.Id, Coins = coins, Items = items, Loyalty = loyalty }));
                }
            }

            if (quest.Claimed >= quest.Winners) End(quest, AllClaimed);
        }

        private void GoalProgress(StreamerQuest quest, Character character, long amount)
        {
            bool finished;
            lock (mutex)
            {
                if (quest.State != Running) return;
                var counted = Math.Min(amount, quest.Amount - quest.Progress);
                if (counted <= 0) return;
                quest.Progress += counted;
                EntryOf(quest, character).Progress += counted;
                finished = quest.Progress >= quest.Amount;
            }

            if (finished) End(quest, Done);
        }

        private void EndOverdue()
        {
            try
            {
                var now = DateTime.UtcNow;
                foreach (var quest in gameData.GetAllStreamerQuests().Where(q => q.State == Running && q.Ends <= now).ToList())
                {
                    End(quest, OutOfTime);
                }
            }
            catch (Exception exc)
            {
                logger.LogError("Ending overdue streamer quests failed: " + exc);
            }
        }

        /// <summary>Ends a quest: a goal that was done pays its helpers, then what is left goes back, and the game hears.</summary>
        private void End(StreamerQuest quest, int state)
        {
            var rewards = new List<StreamerQuestReward>();
            lock (mutex)
            {
                if (quest.State != Running) return;
                quest.State = state;
                quest.Completed = DateTime.UtcNow;

                if (quest.Kind == Goal && state == Done) ShareGoal(quest, rewards);
                ReturnLeftovers(quest);
                gameData.Touch();
            }

            var session = gameData.GetOwnedSessionByUserId(quest.StreamerUserId);
            if (session != null)
            {
                gameData.EnqueueGameEvent(gameData.CreateSessionEvent(GameEventType.StreamerQuestEnded, session,
                    new StreamerQuestEnded { Quest = Info(quest), Rewards = rewards.ToArray() }));
            }
        }

        /// <summary>A done goal: coins and points by share (each helper gets something), the item to the top helpers.</summary>
        private void ShareGoal(StreamerQuest quest, List<StreamerQuestReward> rewards)
        {
            var entries = gameData.GetStreamerQuestEntries(quest.Id).Where(e => e.Progress > 0).OrderByDescending(e => e.Progress).ToList();
            var total = Math.Max(1, entries.Sum(e => e.Progress));
            for (var i = 0; i < entries.Count; ++i)
            {
                var entry = entries[i];
                var character = gameData.GetCharacter(entry.CharacterId);
                if (character == null) continue;
                var coins = Math.Min(quest.EscrowCoins, Math.Max(quest.RewardCoins > 0 ? 1 : 0, quest.RewardCoins * entry.Progress / total));
                var loyalty = Math.Min(quest.EscrowLoyalty, Math.Max(quest.RewardLoyalty > 0 ? 1 : 0, quest.RewardLoyalty * entry.Progress / total));
                var items = i < quest.Winners ? Math.Min(quest.RewardItemAmount, quest.EscrowItems) : 0;
                Pay(quest, character, entry, coins, items, loyalty);
                QuestTracker.StreamQuestHelped(character);
                rewards.Add(new StreamerQuestReward { CharacterId = character.Id, Name = character.Name, Amount = entry.Progress, Coins = coins, Items = items, Loyalty = loyalty });
            }
        }

        /// <summary>Gives a character its part from what was set aside.</summary>
        private void Pay(StreamerQuest quest, Character character, StreamerQuestEntry entry, long coins, long items, long loyalty)
        {
            var coinMoves = coins > 0 ? new[] { new CoinMove(ItemMove.Outside, character.Id, coins) } : Array.Empty<CoinMove>();
            var itemMoves = items > 0 && quest.RewardItemId != null ? new[] { new ItemMove(ItemMove.Outside, character.Id, RewardStack(quest), items) } : Array.Empty<ItemMove>();
            if (coinMoves.Length > 0 || itemMoves.Length > 0)
            {
                var world = new StreamerShopWorld(gameData, inventories).WithCharacter(character.Id);
                var trade = new TradeExecutor(world).Commit(new TradePlan(TradeKind.StreamerQuestReward, itemMoves, coinMoves));
                if (!trade.Ok)
                {
                    // kept aside: it goes back to the streamer with the rest
                    logger.LogWarning("Streamer quest reward for " + character.Name + " failed: " + trade.Reason);
                    coins = 0;
                    items = 0;
                }
            }

            quest.EscrowCoins -= coins;
            quest.EscrowItems -= items;
            entry.Coins += coins;
            entry.Items += items;

            if (loyalty > 0)
            {
                quest.EscrowLoyalty -= loyalty;
                entry.Loyalty += loyalty;
                var points = gameData.GetUserLoyalty(character.UserId, quest.StreamerUserId);
                if (points == null)
                {
                    points = new UserLoyalty { Id = Guid.NewGuid(), Playtime = "00:00:00", StreamerUserId = quest.StreamerUserId, UserId = character.UserId, Level = 1 };
                    gameData.Add(points);
                }
                points.Points += loyalty;
            }

            if (coins > 0) SendCoins(character);
        }

        /// <summary>What was set aside and not given: coins and items to where they came from, points to the week's budget.</summary>
        private void ReturnLeftovers(StreamerQuest quest)
        {
            if (quest.EscrowCoins > 0 && quest.CoinSourceId is Guid coinSource)
            {
                var village = gameData.GetVillageByUserId(quest.StreamerUserId);
                var toCharacter = quest.CoinSource == 2 && gameData.GetCharacter(coinSource) != null;
                var world = new StreamerShopWorld(gameData, inventories);
                Guid to;
                if (toCharacter) { world.WithCharacter(coinSource); to = coinSource; }
                else if (village != null) { world.WithVillage(village); to = village.Id; }
                else to = Guid.Empty;

                if (to != Guid.Empty && new TradeExecutor(world).Commit(new TradePlan(TradeKind.StreamerQuestReturn, null, new[] { new CoinMove(ItemMove.Outside, to, quest.EscrowCoins) })).Ok)
                {
                    quest.EscrowCoins = 0;
                    if (toCharacter) SendCoins(gameData.GetCharacter(coinSource));
                    else
                    {
                        var session = gameData.GetOwnedSessionByUserId(quest.StreamerUserId);
                        if (session != null) sessionManager.SendVillageInfo(session);
                    }
                }
                else logger.LogError("Could not return " + quest.EscrowCoins + " coins of streamer quest " + quest.Id);
            }

            if (quest.EscrowItems > 0 && quest.RewardItemId != null)
            {
                var stack = RewardStack(quest);
                var delivered = false;
                if (quest.ItemSourceCharacterId is Guid characterId && gameData.GetCharacter(characterId)?.UserId == quest.StreamerUserId)
                {
                    var world = new StreamerShopWorld(gameData, inventories).WithCharacter(characterId);
                    delivered = new TradeExecutor(world).Commit(new TradePlan(TradeKind.StreamerQuestReturn, new[] { new ItemMove(ItemMove.Outside, characterId, stack, quest.EscrowItems) })).Ok;
                }
                if (!delivered)
                {
                    var world = new StreamerShopWorld(gameData, inventories).WithStash(quest.StreamerUserId);
                    delivered = new TradeExecutor(world).Commit(new TradePlan(TradeKind.StreamerQuestReturn, new[] { new ItemMove(ItemMove.Outside, quest.StreamerUserId, stack, quest.EscrowItems) })).Ok;
                }
                if (delivered) quest.EscrowItems = 0;
                else logger.LogError("Could not return " + quest.EscrowItems + " items of streamer quest " + quest.Id);
            }

            if (quest.EscrowLoyalty > 0)
            {
                // back to the budget only within the week it came from
                if (quest.LoyaltyWeek == WeekOf(DateTime.UtcNow))
                {
                    var budget = BudgetOf(quest.StreamerUserId, quest.LoyaltyWeek);
                    budget.Spent = Math.Max(0, budget.Spent - quest.EscrowLoyalty);
                }
                quest.EscrowLoyalty = 0;
            }
        }

        // ---- Helpers ---------------------------------------------------------------------------------------

        private StreamerQuestInfo Info(StreamerQuest q)
        {
            var entries = gameData.GetStreamerQuestEntries(q.Id);
            var top = q.Kind == Bounty
                ? entries.Where(e => e.Completed != null).OrderBy(e => e.Completed)
                : entries.Where(e => e.Progress > 0).OrderByDescending(e => e.Progress);
            return new StreamerQuestInfo
            {
                Id = q.Id, Kind = q.Kind, Name = q.Name, Description = q.Description, Objective = q.Objective, Skill = q.Skill,
                Amount = q.Amount, Progress = q.Progress, Winners = q.Winners, Claimed = q.Claimed, State = q.State, Ends = q.Ends,
                Minutes = (int)Math.Round((q.Ends - q.Started).TotalMinutes),
                RewardCoins = q.RewardCoins,
                RewardItemId = q.RewardItemId,
                RewardItemName = q.RewardItemId != null ? (q.RewardItemName ?? gameData.GetItem(q.RewardItemId.Value)?.Name) : null,
                RewardItemAmount = q.RewardItemAmount,
                RewardLoyalty = q.RewardLoyalty,
                Helpers = entries.Count(e => e.Progress > 0),
                TopHelpers = top.Take(TopHelpersShown).Select(e => new StreamQuestHelper
                {
                    CharacterId = e.CharacterId, Name = gameData.GetCharacter(e.CharacterId)?.Name, Amount = e.Progress, Coins = e.Coins,
                }).ToArray(),
            };
        }

        private StreamerQuestEntry EntryOf(StreamerQuest quest, Character character)
        {
            var entry = gameData.GetStreamerQuestEntries(quest.Id).FirstOrDefault(e => e.CharacterId == character.Id);
            if (entry != null) return entry;
            entry = new StreamerQuestEntry { Id = Guid.NewGuid(), StreamerQuestId = quest.Id, CharacterId = character.Id };
            gameData.Add(entry);
            return entry;
        }

        private static StackKey RewardStack(StreamerQuest q) =>
            new StackKey(q.RewardItemId.Value, q.RewardItemTag, q.RewardItemEnchantment, q.RewardItemTransmogrificationId, q.RewardItemName, q.RewardItemFlags);

        private long BudgetLeft(Guid streamerUserId)
        {
            var week = WeekOf(DateTime.UtcNow);
            var spent = gameData.GetStreamerQuestBudgets(streamerUserId).FirstOrDefault(b => b.Week == week)?.Spent ?? 0;
            return Math.Max(0, WeeklyLoyaltyBudget - spent);
        }

        private StreamerQuestBudget BudgetOf(Guid streamerUserId, string week)
        {
            var budget = gameData.GetStreamerQuestBudgets(streamerUserId).FirstOrDefault(b => b.Week == week);
            if (budget != null) return budget;
            budget = new StreamerQuestBudget { Id = Guid.NewGuid(), StreamerUserId = streamerUserId, Week = week };
            gameData.Add(budget);
            return budget;
        }

        private IEnumerable<Character> OwnCharacters(GameSession session) =>
            (gameData.GetActiveSessionCharacters(session) ?? (IReadOnlyList<Character>)Array.Empty<Character>()).Where(x => x.UserId == session.UserId);

        private void SendCoins(Character character)
        {
            var resources = character != null ? gameData.GetResources(character) : null;
            var session = character?.UserIdLock is Guid owner ? gameData.GetSessionByUserId(owner) : null;
            if (resources == null || session == null) return;
            gameData.EnqueueGameEvent(gameData.CreateSessionEvent(GameEventType.ResourceUpdate, session, new ResourceUpdate
            {
                CharacterId = character.Id,
                FishAmount = resources.Fish,
                OreAmount = resources.Ore,
                WheatAmount = resources.Wheat,
                WoodAmount = resources.Wood,
                CoinsAmount = resources.Coins
            }));
        }

        /// <summary>One line, trimmed, at most so long.</summary>
        private static string Clean(string text, int max)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            var line = string.Join(" ", text.Split(new[] { '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries)).Trim();
            return line.Length > max ? line.Substring(0, max).TrimEnd() : line;
        }

        private static string WeekOf(DateTime utc) =>
            ISOWeek.GetYear(utc).ToString(CultureInfo.InvariantCulture) + "-W" + ISOWeek.GetWeekOfYear(utc).ToString("00", CultureInfo.InvariantCulture);

        private static StreamerQuestResult Fail(string error) => new StreamerQuestResult { Ok = false, Error = error };

        public void Dispose() => timer.Dispose();
    }
}

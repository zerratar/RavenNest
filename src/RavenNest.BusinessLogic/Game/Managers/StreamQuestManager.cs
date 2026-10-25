using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Data;
using RavenNest.DataModels;
using RavenNest.Models;
using Skill = RavenNest.Models.Skill;
using GameSession = RavenNest.DataModels.GameSession;

namespace RavenNest.BusinessLogic.Game
{
    /// <summary>
    /// Stream quests (phase 2 of docs/quests-design.md in the game repository): one goal the whole stream works on
    /// together. The streamer picks one of three offers a day (build mode's Quests tab, or !quest start), scaled to the
    /// players in the stream; everyone adds to it by playing (counted as personal quests are, through
    /// <see cref="QuestTracker"/>). It has a time limit: done in time, the village gets coins and a resource and the
    /// helpers share a pot of coins by how much each added, with a little loyalty with the streamer; out of time, it
    /// simply ends. One at a time per stream.
    /// </summary>
    public class StreamQuestManager : IDisposable
    {
        public const string Gather = QuestManager.Gather;
        public const string Kill = QuestManager.Kill;
        public const string Make = "make";
        public const string Levels = "levels";
        public const string Raid = QuestManager.Raid;
        public const string Dungeon = QuestManager.Dungeon;

        public const int Offered = 0, Running = 1, Done = 2, OutOfTime = 3, Stopped = 4;
        private const int Offers = 3;
        private const int MinPlayers = 3;
        private const long MinHelperCoins = 100;
        private const long HelperLoyaltyPoints = 10;
        private const int TopHelpersShown = 5;

        /// <summary>A kind of stream quest; the target and rewards grow with the players in the stream.</summary>
        private sealed class Template
        {
            public string Key;
            public string Objective;
            public int Skill = -1;
            public long PerPlayer;
            public long Min;
            public int Minutes;

            /// <summary>1 wood, 2 ore, 3 fish, 4 wheat: the village gets as much again of what was gathered.</summary>
            public int Resource;
            public long VillageCoins;
            public long VillageCoinsPerPlayer;
            public long HelperCoinsPerPlayer;
        }

        // First numbers, set 2026-10-25, to be balanced. A gathering round comes every 10 seconds per player.
        private static readonly Template[] Catalog =
        {
            Gathering("stream_wood", Skill.Woodcutting, 1), Gathering("stream_ore", Skill.Mining, 2),
            Gathering("stream_fish", Skill.Fishing, 3), Gathering("stream_wheat", Skill.Farming, 4),
            new Template { Key = "stream_kill", Objective = Kill, PerPlayer = 60, Min = 300, Minutes = 60, VillageCoins = 1500, VillageCoinsPerPlayer = 50, HelperCoinsPerPlayer = 400 },
            new Template { Key = "stream_make", Objective = Make, PerPlayer = 5, Min = 30, Minutes = 60, VillageCoins = 1000, VillageCoinsPerPlayer = 40, HelperCoinsPerPlayer = 300 },
            new Template { Key = "stream_levels", Objective = Levels, PerPlayer = 1, Min = 10, Minutes = 120, VillageCoins = 2000, VillageCoinsPerPlayer = 60, HelperCoinsPerPlayer = 500 },
            new Template { Key = "stream_raid", Objective = Raid, PerPlayer = 0, Min = 3, Minutes = 120, VillageCoins = 2500, VillageCoinsPerPlayer = 60, HelperCoinsPerPlayer = 500 },
            new Template { Key = "stream_dungeon", Objective = Dungeon, PerPlayer = 0, Min = 2, Minutes = 120, VillageCoins = 2500, VillageCoinsPerPlayer = 60, HelperCoinsPerPlayer = 500 },
        };

        private static Template Gathering(string key, Skill skill, int resource) => new Template
        {
            Key = key, Objective = Gather, Skill = (int)skill, PerPlayer = 60, Min = 300, Minutes = 60, Resource = resource,
            VillageCoins = 1000, VillageCoinsPerPlayer = 40, HelperCoinsPerPlayer = 300,
        };

        /// <summary>A stream's running quest and its helpers.</summary>
        private sealed class Run
        {
            public StreamQuest Quest;
            public readonly ConcurrentDictionary<Guid, StreamQuestContribution> Helpers = new ConcurrentDictionary<Guid, StreamQuestContribution>();
        }

        private readonly ConcurrentDictionary<Guid, Run> running = new ConcurrentDictionary<Guid, Run>();
        private readonly GameData gameData;
        private readonly ILogger<StreamQuestManager> logger;
        private readonly Timer timer;

        public StreamQuestManager(GameData gameData, ILogger<StreamQuestManager> logger)
        {
            this.gameData = gameData;
            this.logger = logger;

            // the quests that were running when the server stopped run on
            foreach (var quest in gameData.GetAllStreamQuests().Where(q => q.State == Running))
            {
                var run = new Run { Quest = quest };
                foreach (var c in gameData.GetStreamQuestContributions(quest.Id)) run.Helpers[c.CharacterId] = c;
                running[quest.StreamerUserId] = run;
            }

            QuestTracker.StreamListener = this;
            timer = new Timer(_ => EndOverdue(), null, 15_000, 15_000);
        }

        // ---- What the game asks ----------------------------------------------------------------------

        /// <summary>The stream's running quest, and today's offers scaled to the players in the stream now.</summary>
        public StreamQuestState GetState(SessionToken token)
        {
            var session = token != null ? gameData.GetSession(token.SessionId) : null;
            if (session == null) return null;
            var active = running.TryGetValue(token.UserId, out var run) ? Info(run) : null;
            var players = PlayersIn(session);
            return new StreamQuestState
            {
                Active = active,
                Offers = OffersFor(token.UserId).Select((t, i) => Preview(t, i, players)).ToArray(),
            };
        }

        public StreamQuestResult Start(SessionToken token, int slot)
        {
            var session = token != null ? gameData.GetSession(token.SessionId) : null;
            if (session == null) return new StreamQuestResult { Error = "unknown" };
            var offers = OffersFor(token.UserId);
            if (slot < 0 || slot >= offers.Count) return new StreamQuestResult { Error = "unknown" };

            lock (running)
            {
                if (running.TryGetValue(token.UserId, out var current)) return new StreamQuestResult { Error = "running", Quest = Info(current) };

                var template = offers[slot];
                var players = PlayersIn(session);
                var now = DateTime.UtcNow;
                var quest = new StreamQuest
                {
                    Id = Guid.NewGuid(),
                    StreamerUserId = token.UserId,
                    Key = template.Key,
                    Objective = template.Objective,
                    Skill = template.Skill,
                    Amount = TargetFor(template, players),
                    State = Running,
                    Players = players,
                    Started = now,
                    Ends = now.AddMinutes(template.Minutes),
                    RewardVillageCoins = template.VillageCoins + template.VillageCoinsPerPlayer * players,
                    RewardResource = template.Resource,
                    RewardHelperCoins = template.HelperCoinsPerPlayer * players,
                };
                if (template.Resource > 0) quest.RewardResourceAmount = quest.Amount;
                gameData.Add(quest);
                var run = new Run { Quest = quest };
                running[token.UserId] = run;
                return new StreamQuestResult { Ok = true, Quest = Info(run) };
            }
        }

        public StreamQuestResult Stop(SessionToken token)
        {
            if (token == null || !running.TryGetValue(token.UserId, out var run)) return new StreamQuestResult { Error = "none" };
            End(run, Stopped);
            return new StreamQuestResult { Ok = true, Quest = Info(run) };
        }

        // ---- Progress -------------------------------------------------------------------------------------

        internal void Progress(Character character, string objective, int skill, long amount)
        {
            if (character?.UserIdLock == null || amount <= 0) return;
            if (!running.TryGetValue(character.UserIdLock.Value, out var run)) return;

            var quest = run.Quest;
            var matches = quest.Objective == objective
                || (quest.Objective == Make && (objective == QuestManager.Craft || objective == QuestManager.Cook || objective == QuestManager.Brew));
            if (!matches || (quest.Skill >= 0 && skill >= 0 && quest.Skill != skill)) return;

            try
            {
                var finished = false;
                lock (run)
                {
                    if (quest.State != Running) return;
                    if (quest.Ends <= DateTime.UtcNow)
                    {
                        finished = true;
                    }
                    else
                    {
                        var counted = Math.Min(amount, quest.Amount - quest.Progress);
                        if (counted <= 0) return;
                        quest.Progress += counted;
                        var helper = run.Helpers.GetOrAdd(character.Id, id =>
                        {
                            var c = new StreamQuestContribution { Id = Guid.NewGuid(), StreamQuestId = quest.Id, CharacterId = id };
                            gameData.Add(c);
                            return c;
                        });
                        helper.Amount += counted;
                        finished = quest.Progress >= quest.Amount;
                    }
                }

                if (finished) End(run, quest.Progress >= quest.Amount ? Done : OutOfTime);
            }
            catch (Exception exc)
            {
                logger.LogError("Stream quest progress failed: " + exc);
            }
        }

        private void EndOverdue()
        {
            try
            {
                var now = DateTime.UtcNow;
                foreach (var run in running.Values)
                {
                    if (run.Quest.Ends <= now) End(run, run.Quest.Progress >= run.Quest.Amount ? Done : OutOfTime);
                }
            }
            catch (Exception exc)
            {
                logger.LogError("Ending overdue stream quests failed: " + exc);
            }
        }

        /// <summary>Ends a quest: done pays the village and the helpers; every ending tells the game.</summary>
        private void End(Run run, int state)
        {
            var quest = run.Quest;
            lock (run)
            {
                if (quest.State != Running) return;
                quest.State = state;
                quest.Completed = DateTime.UtcNow;
            }
            running.TryRemove(new KeyValuePair<Guid, Run>(quest.StreamerUserId, run));

            var session = gameData.GetOwnedSessionByUserId(quest.StreamerUserId);
            if (state == Done) Reward(run, session);
            gameData.Touch();

            if (session != null)
            {
                var helpers = run.Helpers.Values.OrderByDescending(h => h.Amount).Select(Helper).ToArray();
                gameData.EnqueueGameEvent(gameData.CreateSessionEvent(GameEventType.StreamQuestEnded, session,
                    new StreamQuestEnded { Quest = Info(run), Helpers = helpers }));
            }
        }

        private void Reward(Run run, GameSession session)
        {
            var quest = run.Quest;
            var village = session != null ? gameData.GetVillageBySession(session) : null;
            var resources = village != null ? gameData.GetResources(village.ResourcesId) : null;
            if (resources != null)
            {
                resources.Coins += quest.RewardVillageCoins;
                switch (quest.RewardResource)
                {
                    case 1: resources.Wood += quest.RewardResourceAmount; break;
                    case 2: resources.Ore += quest.RewardResourceAmount; break;
                    case 3: resources.Fish += quest.RewardResourceAmount; break;
                    case 4: resources.Wheat += quest.RewardResourceAmount; break;
                }
            }

            // the pot by share, and never less than a small sum for anyone who helped
            var total = Math.Max(1, run.Helpers.Values.Sum(h => h.Amount));
            foreach (var helper in run.Helpers.Values)
            {
                helper.Coins = Math.Max(MinHelperCoins, (long)Math.Round((double)quest.RewardHelperCoins * helper.Amount / total));
                var character = gameData.GetCharacter(helper.CharacterId);
                if (character == null) continue;
                var coins = gameData.GetResources(character);
                if (coins != null) coins.Coins += helper.Coins;

                var loyalty = gameData.GetUserLoyalty(character.UserId, quest.StreamerUserId);
                if (loyalty == null)
                {
                    loyalty = new UserLoyalty
                    {
                        Id = Guid.NewGuid(), Playtime = "00:00:00", StreamerUserId = quest.StreamerUserId, UserId = character.UserId, Level = 1,
                    };
                    gameData.Add(loyalty);
                }
                loyalty.Points += HelperLoyaltyPoints;
            }
        }

        // ---- Helpers ---------------------------------------------------------------------------------------

        /// <summary>Today's three offers for a streamer: the same all day.</summary>
        private static List<Template> OffersFor(Guid streamerUserId)
        {
            var day = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var seed = streamerUserId.GetHashCode();
            foreach (var c in day) seed = seed * 31 + c;
            var random = new Random(seed);
            return Catalog.OrderBy(_ => random.Next()).Take(Offers).ToList();
        }

        private static long TargetFor(Template t, int players) => Math.Max(t.Min, t.PerPlayer * players) / 10 * 10 + (t.PerPlayer == 0 ? t.Min % 10 : 0);

        private int PlayersIn(GameSession session) => Math.Max(MinPlayers, gameData.GetActiveSessionCharacters(session).Count);

        private static StreamQuestInfo Preview(Template t, int slot, int players)
        {
            var amount = TargetFor(t, players);
            return new StreamQuestInfo
            {
                Key = t.Key, Slot = slot, Objective = t.Objective, Skill = t.Skill, Amount = amount, State = Offered, Minutes = t.Minutes,
                RewardVillageCoins = t.VillageCoins + t.VillageCoinsPerPlayer * players,
                RewardResource = t.Resource, RewardResourceAmount = t.Resource > 0 ? amount : 0,
                RewardHelperCoins = t.HelperCoinsPerPlayer * players,
                TopHelpers = Array.Empty<StreamQuestHelper>(),
            };
        }

        private StreamQuestInfo Info(Run run)
        {
            var q = run.Quest;
            return new StreamQuestInfo
            {
                Id = q.Id, Key = q.Key, Objective = q.Objective, Skill = q.Skill, Amount = q.Amount, Progress = q.Progress,
                State = q.State, Minutes = (int)Math.Round((q.Ends - q.Started).TotalMinutes), Ends = q.Ends,
                RewardVillageCoins = q.RewardVillageCoins, RewardResource = q.RewardResource, RewardResourceAmount = q.RewardResourceAmount,
                RewardHelperCoins = q.RewardHelperCoins,
                Helpers = run.Helpers.Count,
                TopHelpers = run.Helpers.Values.OrderByDescending(h => h.Amount).Take(TopHelpersShown).Select(Helper).ToArray(),
            };
        }

        private StreamQuestHelper Helper(StreamQuestContribution c) => new StreamQuestHelper
        {
            CharacterId = c.CharacterId,
            Name = gameData.GetCharacter(c.CharacterId)?.Name,
            Amount = c.Amount,
            Coins = c.Coins,
        };

        public void Dispose() => timer.Dispose();
    }
}

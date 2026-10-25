using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Data;
using RavenNest.DataModels;
using RavenNest.Models;

namespace RavenNest.BusinessLogic.Game
{
    /// <summary>
    /// The streamer's quest log (phase 5 of docs/quests-design.md in the game repository): story lines given to the
    /// streamer as the village's leader (VillageStories), one under way at a time. The viewers work on the steps by
    /// playing, by bringing items (!quest give) and by finding things; the streamer talks to the NPC and makes the
    /// choices. Each step moved or finished tells the game (VillageStoryProgress); a finished story pays the helpers,
    /// unlocks its decor piece, its unlocks and its perk for the streamer for good (VillageStoryCompleted).
    /// </summary>
    public class VillageStoryManager : IDisposable
    {
        public const int NotBegun = 0, Active = 1, Done = 2, GivenUp = 4;
        private const int TopHelpersShown = 5;
        private const long HelperLoyaltyPoints = 10;

        private readonly GameData gameData;
        private readonly PlayerInventoryProvider inventories;
        private readonly VillageDecorManager decorManager;
        private readonly ILogger<VillageStoryManager> logger;
        private readonly object mutex = new object();
        private readonly Random random = new Random();
        private readonly Timer timer;

        public VillageStoryManager(GameData gameData, PlayerInventoryProvider inventories, VillageDecorManager decorManager, ILogger<VillageStoryManager> logger)
        {
            this.gameData = gameData;
            this.inventories = inventories;
            this.decorManager = decorManager;
            this.logger = logger;
            QuestTracker.Stories = this;
            timer = new Timer(_ => CheckViewers(), null, 20_000, 20_000);
        }

        // ---- What the game asks ----------------------------------------------------------------------

        public VillageLog GetLog(SessionToken token)
        {
            var session = token != null ? gameData.GetSession(token.SessionId) : null;
            if (session == null) return null;
            return LogOf(session.UserId);
        }

        /// <summary>The story lines and the unlocks of a streamer.</summary>
        public VillageLog LogOf(Guid streamerUserId)
        {
            var rows = gameData.GetVillageStories(streamerUserId);
            return new VillageLog
            {
                Stories = VillageStories.All.Select(s =>
                {
                    var row = rows.Where(r => r.Key == s.Key && r.State != GivenUp).OrderByDescending(r => r.Started).FirstOrDefault();
                    return row != null ? Info(row) : new VillageStoryInfo { Key = s.Key, State = NotBegun, Steps = s.Steps.Length, Kind = s.Steps[0].Kind };
                }).ToArray(),
                Unlocks = gameData.GetStreamerUnlocks(streamerUserId).Select(u => u.Key).ToArray(),
            };
        }

        /// <summary>Begins a story line; one at a time, each once.</summary>
        public VillageStoryResult Begin(SessionToken token, string key)
        {
            var session = token != null ? gameData.GetSession(token.SessionId) : null;
            if (session == null) return Fail("notinsession");
            var story = VillageStories.Get(key);
            if (story == null) return Fail("unknown");
            lock (mutex)
            {
                var rows = gameData.GetVillageStories(session.UserId);
                var active = rows.FirstOrDefault(r => r.State == Active);
                if (active != null) return new VillageStoryResult { Error = active.Key == key ? "taken" : "busy", Story = Info(active) };
                if (rows.Any(r => r.Key == key && r.State == Done)) return Fail("done");

                var row = new VillageStory { Id = Guid.NewGuid(), StreamerUserId = session.UserId, Key = key, State = Active, Started = DateTime.UtcNow };
                gameData.Add(row);
                return new VillageStoryResult { Ok = true, Story = Info(row) };
            }
        }

        /// <summary>The streamer talks to the NPC: a talk step is done.</summary>
        public VillageStoryResult Talk(SessionToken token, string key)
        {
            var row = ActiveOf(token, key, out var story, out var error);
            if (row == null) return Fail(error);
            lock (mutex)
            {
                if (story.Steps[row.Step].Kind != VillageStories.Talk) return new VillageStoryResult { Error = "notstep", Story = Info(row) };
                StepDone(row, story);
                return new VillageStoryResult { Ok = true, Story = Info(row) };
            }
        }

        /// <summary>The streamer makes the story's choice (from 1).</summary>
        public VillageStoryResult Choose(SessionToken token, string key, int option)
        {
            var row = ActiveOf(token, key, out var story, out var error);
            if (row == null) return Fail(error);
            lock (mutex)
            {
                var step = story.Steps[row.Step];
                if (step.Kind != VillageStories.Choice) return new VillageStoryResult { Error = "notstep", Story = Info(row) };
                if (option < 1 || option > step.Options) return new VillageStoryResult { Error = "unknown", Story = Info(row) };
                row.Choices = string.IsNullOrEmpty(row.Choices) ? row.Step + "=" + option : row.Choices + "," + row.Step + "=" + option;
                StepDone(row, story);
                return new VillageStoryResult { Ok = true, Story = Info(row) };
            }
        }

        /// <summary>A viewer brings the delivery step's item from their inventory (!quest give).</summary>
        public VillageStoryResult Give(SessionToken token, Guid characterId)
        {
            var character = gameData.GetCharacter(characterId);
            if (character == null || token == null || character.UserIdLock != token.UserId) return Fail("notinsession");
            lock (mutex)
            {
                var row = gameData.GetVillageStories(token.UserId).FirstOrDefault(r => r.State == Active);
                var story = row != null ? VillageStories.Get(row.Key) : null;
                if (story == null) return Fail("none");
                var step = story.Steps[row.Step];
                if (step.Kind != VillageStories.Deliver) return new VillageStoryResult { Error = "notstep", Story = Info(row) };

                var item = gameData.GetItems().FirstOrDefault(i => string.Equals(i.Name, step.Target, StringComparison.OrdinalIgnoreCase));
                if (item == null) return new VillageStoryResult { Error = "unknown", Story = Info(row) };

                var inventory = inventories.Get(character.Id);
                var needed = step.Amount - row.Progress;
                long given = 0;
                foreach (var stack in inventory.GetUnequippedItems().Where(x => x.ItemId == item.Id && !x.Soulbound).ToList())
                {
                    if (given >= needed) break;
                    var take = Math.Min(stack.Amount, needed - given);
                    if (take > 0 && inventory.RemoveItem(stack, take)) given += take;
                }
                if (given == 0) return new VillageStoryResult { Error = "nothing", Story = Info(row) };

                Count(row, story, character, given);
                gameData.Touch();
                return new VillageStoryResult { Ok = true, Story = Info(row), Given = given };
            }
        }

        public VillageStoryResult Abandon(SessionToken token, string key)
        {
            var row = ActiveOf(token, key, out _, out var error);
            if (row == null) return Fail(error);
            row.State = GivenUp;
            row.Completed = DateTime.UtcNow;
            return new VillageStoryResult { Ok = true, Story = Info(row) };
        }

        /// <summary>The unlocks of a streamer, for others to look at (titles, islands).</summary>
        public bool HasUnlock(Guid streamerUserId, string key) => gameData.GetStreamerUnlocks(streamerUserId).Any(u => u.Key == key);

        // ---- Progress -------------------------------------------------------------------------------------

        /// <summary>Something a character did in a stream: counts toward the stream's story step of the same kind.</summary>
        internal void Activity(Character character, string objective, int skill, long amount)
        {
            if (character?.UserIdLock == null || amount <= 0) return;
            var row = gameData.GetVillageStories(character.UserIdLock.Value).FirstOrDefault(r => r.State == Active);
            if (row == null) return;
            var story = VillageStories.Get(row.Key);
            if (story == null) return;

            try
            {
                lock (mutex)
                {
                    if (row.State != Active) return;
                    var step = story.Steps[row.Step];
                    if (step.Kind == objective && (step.Skill < 0 || skill < 0 || step.Skill == skill))
                    {
                        Count(row, story, character, amount);
                    }
                    else if (step.Kind == VillageStories.Find && objective == QuestManager.Gather && step.Skill == skill)
                    {
                        // a gathering round of the skill: sometimes it finds the story's thing
                        for (var i = 0; i < amount; ++i)
                        {
                            if (random.NextDouble() >= step.Chance) continue;
                            var stepBefore = row.Step;
                            Count(row, story, character, 1, found: true);
                            if (row.Step != stepBefore || row.State != Active) break;
                        }
                    }
                }
            }
            catch (Exception exc)
            {
                logger.LogError("Story progress failed for " + character.Name + ": " + exc);
            }
        }

        /// <summary>
        /// An item gathering dropped: when the stream's delivery step wants it, it goes to the village instead of the
        /// inventory. Returns whether it was taken.
        /// </summary>
        internal bool ItemFound(Character character, Guid itemId)
        {
            if (character?.UserIdLock == null) return false;
            var row = gameData.GetVillageStories(character.UserIdLock.Value).FirstOrDefault(r => r.State == Active);
            var story = row != null ? VillageStories.Get(row.Key) : null;
            if (story == null) return false;
            lock (mutex)
            {
                if (row.State != Active) return false;
                var step = story.Steps[row.Step];
                if (step.Kind != VillageStories.Deliver || row.Progress >= step.Amount) return false;
                var item = gameData.GetItem(itemId);
                if (item == null || !string.Equals(item.Name, step.Target, StringComparison.OrdinalIgnoreCase)) return false;
                Count(row, story, character, 1);
                return true;
            }
        }

        /// <summary>The viewers step: so many players in the stream at once, looked at now and then.</summary>
        private void CheckViewers()
        {
            try
            {
                foreach (var row in gameData.GetAllVillageStories().Where(r => r.State == Active).ToList())
                {
                    var story = VillageStories.Get(row.Key);
                    if (story == null || story.Steps[row.Step].Kind != VillageStories.Viewers) continue;
                    var session = gameData.GetOwnedSessionByUserId(row.StreamerUserId);
                    if (session == null) continue;
                    var count = gameData.GetActiveSessionCharacters(session).Count;
                    lock (mutex)
                    {
                        if (row.State != Active || story.Steps[row.Step].Kind != VillageStories.Viewers) continue;
                        if (count > row.Progress) row.Progress = Math.Min(story.Steps[row.Step].Amount, count);
                        if (row.Progress >= story.Steps[row.Step].Amount) StepDone(row, story);
                    }
                }
            }
            catch (Exception exc)
            {
                logger.LogError("Checking the story viewers failed: " + exc);
            }
        }

        /// <summary>Adds to the current step and its helper; a full step moves the story on.</summary>
        private void Count(VillageStory row, VillageStories.Story story, Character character, long amount, bool found = false)
        {
            var step = story.Steps[row.Step];
            var counted = Math.Min(amount, step.Amount - row.Progress);
            if (counted <= 0) return;
            row.Progress += counted;

            var helper = gameData.GetVillageStoryHelpers(row.Id).FirstOrDefault(h => h.Step == row.Step && h.CharacterId == character.Id);
            if (helper == null)
            {
                helper = new VillageStoryHelper { Id = Guid.NewGuid(), VillageStoryId = row.Id, Step = row.Step, CharacterId = character.Id };
                gameData.Add(helper);
            }
            helper.Amount += counted;
            helper.Updated = DateTime.UtcNow;

            if (found)
            {
                // the finder is named in chat
                Tell(row, new VillageStoryProgress { Story = Info(row), StepDone = -1, FinderId = character.Id, FinderName = character.Name });
            }

            if (row.Progress >= step.Amount) StepDone(row, story);
        }

        private void StepDone(VillageStory row, VillageStories.Story story)
        {
            var done = row.Step;
            if (row.Step + 1 < story.Steps.Length)
            {
                row.Step++;
                row.Progress = 0;
                Tell(row, new VillageStoryProgress { Story = Info(row), StepDone = done });
                return;
            }

            row.State = Done;
            row.Completed = DateTime.UtcNow;
            Finish(row, story);
        }

        /// <summary>The story is told to its end: the unlocks, the perk, the decor piece and the helpers' shares.</summary>
        private void Finish(VillageStory row, VillageStories.Story story)
        {
            var unlocks = new List<string>(story.Unlocks ?? Array.Empty<string>());
            if (!string.IsNullOrEmpty(story.Perk))
            {
                unlocks.Add(story.PerkChoiceStep >= 0 ? story.Perk + ChoiceAt(row, story.PerkChoiceStep) : story.Perk);
            }

            var owned = gameData.GetStreamerUnlocks(row.StreamerUserId).Select(u => u.Key).ToHashSet();
            foreach (var key in unlocks.Where(k => !owned.Contains(k)))
            {
                gameData.Add(new StreamerUnlock { Id = Guid.NewGuid(), StreamerUserId = row.StreamerUserId, Key = key, Source = row.Key, Created = DateTime.UtcNow });
            }

            var session = gameData.GetOwnedSessionByUserId(row.StreamerUserId);
            var decor = session != null ? decorManager.UnlockForQuest(session.Id, row.Key) : null;

            // the helpers share the coins by how much each did, over every step; each gets a little loyalty too
            var helpers = gameData.GetVillageStoryHelpers(row.Id).GroupBy(h => h.CharacterId)
                .Select(g => (characterId: g.Key, amount: g.Sum(h => h.Amount))).OrderByDescending(x => x.amount).ToList();
            var pool = story.HelperCoinsPerHelper * helpers.Count;
            var total = Math.Max(1, helpers.Sum(x => x.amount));
            var paid = new List<StreamQuestHelper>();
            foreach (var (characterId, amount) in helpers)
            {
                var character = gameData.GetCharacter(characterId);
                if (character == null) continue;
                var coins = Math.Max(100, pool * amount / total);
                var resources = gameData.GetResources(character);
                if (resources != null) resources.Coins += coins;

                var loyalty = gameData.GetUserLoyalty(character.UserId, row.StreamerUserId);
                if (loyalty == null)
                {
                    loyalty = new UserLoyalty { Id = Guid.NewGuid(), Playtime = "00:00:00", StreamerUserId = row.StreamerUserId, UserId = character.UserId, Level = 1 };
                    gameData.Add(loyalty);
                }
                loyalty.Points += HelperLoyaltyPoints;
                paid.Add(new StreamQuestHelper { CharacterId = characterId, Name = character.Name, Amount = amount, Coins = coins });
            }

            gameData.Touch();
            if (session != null)
            {
                gameData.EnqueueGameEvent(gameData.CreateSessionEvent(GameEventType.VillageStoryCompleted, session,
                    new VillageStoryCompleted { Story = Info(row), Unlocks = unlocks.ToArray(), DecorUnlocked = decor, Helpers = paid.ToArray() }));
            }
        }

        // ---- Helpers ---------------------------------------------------------------------------------------

        private VillageStory ActiveOf(SessionToken token, string key, out VillageStories.Story story, out string error)
        {
            story = null;
            error = null;
            var session = token != null ? gameData.GetSession(token.SessionId) : null;
            if (session == null) { error = "notinsession"; return null; }
            story = VillageStories.Get(key);
            if (story == null) { error = "unknown"; return null; }
            var row = gameData.GetVillageStories(session.UserId).FirstOrDefault(r => r.Key == key && r.State == Active);
            if (row == null) error = "none";
            return row;
        }

        private static int ChoiceAt(VillageStory row, int step)
        {
            foreach (var part in (row.Choices ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = part.Split('=');
                if (kv.Length == 2 && int.TryParse(kv[0], out var s) && s == step && int.TryParse(kv[1], out var option)) return option;
            }
            return 1;
        }

        private VillageStoryInfo Info(VillageStory row)
        {
            var story = VillageStories.Get(row.Key);
            var index = Math.Min(row.Step, story.Steps.Length - 1);
            var step = story.Steps[index];
            var helpers = gameData.GetVillageStoryHelpers(row.Id).Where(h => h.Step == index);
            helpers = step.Kind == VillageStories.Find ? helpers.OrderBy(h => h.Updated) : helpers.OrderByDescending(h => h.Amount);
            return new VillageStoryInfo
            {
                Key = row.Key, State = row.State, Step = row.Step, Steps = story.Steps.Length, Kind = step.Kind, Target = step.Target,
                Skill = step.Skill, Amount = step.Amount, Progress = row.State == Done ? step.Amount : row.Progress, Options = step.Options,
                Choices = row.Choices,
                Helpers = helpers.Take(TopHelpersShown).Select(h => new StreamQuestHelper
                {
                    CharacterId = h.CharacterId, Name = gameData.GetCharacter(h.CharacterId)?.Name, Amount = h.Amount,
                }).ToArray(),
            };
        }

        private void Tell(VillageStory row, VillageStoryProgress progress)
        {
            var session = gameData.GetOwnedSessionByUserId(row.StreamerUserId);
            if (session != null) gameData.EnqueueGameEvent(gameData.CreateSessionEvent(GameEventType.VillageStoryProgress, session, progress));
        }

        private static VillageStoryResult Fail(string error) => new VillageStoryResult { Error = error };

        public void Dispose() => timer.Dispose();
    }
}

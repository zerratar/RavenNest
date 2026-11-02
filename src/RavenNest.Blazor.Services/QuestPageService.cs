using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Game;
using RavenNest.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace RavenNest.Blazor.Services
{
    /// <summary>
    /// The dashboard's quest page (/quests): for each of a user's characters its quests today, the weekly and seasonal
    /// ones, the quest chains, the achievements and titles, and the bounties of the stream it plays in; and for a
    /// streamer their village stories, the quests they posted and their best hero mode run. Read only: quests are taken
    /// and given up in the game. Worded as the game words them (GameTexts, exported from the game).
    /// </summary>
    public class QuestPageService
    {
        private readonly GameData gameData;
        private readonly QuestManager quests;
        private readonly AchievementManager achievements;
        private readonly StreamerQuestManager streamerQuests;
        private readonly VillageStoryManager stories;
        private readonly HeroModeManager hero;

        public QuestPageService(GameData gameData, QuestManager quests, AchievementManager achievements,
            StreamerQuestManager streamerQuests, VillageStoryManager stories, HeroModeManager hero)
        {
            this.gameData = gameData;
            this.quests = quests;
            this.achievements = achievements;
            this.streamerQuests = streamerQuests;
            this.stories = stories;
            this.hero = hero;
        }

        public async Task<QuestPageData> GetAsync(Guid userId)
        {
            return await Task.Run(() =>
            {
                var page = new QuestPageData();
                foreach (var c in gameData.GetCharactersByUserId(userId).OrderBy(c => c.CharacterIndex))
                {
                    try { page.Characters.Add(CharacterOf(c)); }
                    catch (Exception exc) { page.Errors.Add(c.Name + ": " + exc.Message); }
                }
                // a streamer: anyone with a town, or a story begun
                var village = gameData.GetVillageByUserId(userId);
                var log = stories.LogOf(userId);
                if (village != null || log.Stories.Any(s => s.State != 0) || log.Unlocks.Length > 0)
                    page.Streamer = StreamerOf(userId, log);
                return page;
            });
        }

        // ---- A character ----------------------------------------------------------------------------------------

        private QuestPageCharacter CharacterOf(DataModels.Character c)
        {
            var list = quests.Peek(c.Id);
            var ach = achievements.Peek(c.Id);
            var streamer = c.UserIdLock != null ? gameData.GetUser(c.UserIdLock.Value) : null;
            var page = new QuestPageCharacter
            {
                Id = c.Id,
                Name = c.Name,
                Index = c.CharacterIndex,
                StreamName = streamer != null ? (string.IsNullOrEmpty(streamer.DisplayName) ? streamer.UserName : streamer.DisplayName) : null,
                Streak = list?.Streak ?? 0,
                DailiesCompleted = list?.DailiesCompleted ?? 0,
                Title = TitleText(ach?.Title),
            };
            if (list != null)
            {
                page.Taken = (list.Active ?? Array.Empty<QuestInfo>()).Select(q => Card(q, null)).ToList();
                page.Offers = (list.Offers ?? Array.Empty<QuestInfo>()).Select(q => Card(q, "!quest accept " + (q.Slot + 1))).ToList();
                page.Weekly = list.Weekly != null ? Card(list.Weekly, list.Weekly.State == 0 ? "!quest accept weekly" : null) : null;
                page.Seasonal = list.Seasonal != null ? Card(list.Seasonal, list.Seasonal.State == 0 ? "!quest accept seasonal" : null) : null;
                page.Chains = (list.Chains ?? Array.Empty<QuestChainInfo>()).Select(ch => Chain(ch, list.Chain)).ToList();
            }
            if (ach != null)
            {
                page.Achievements = (ach.Achievements ?? Array.Empty<AchievementInfo>()).Select(Achievement)
                    .OrderByDescending(a => a.Tier).ThenByDescending(a => a.Share).ToList();
                page.Titles = (ach.Titles ?? Array.Empty<string>()).Select((t, i) => new QuestPageTitle
                {
                    Number = i + 1, Text = TitleText(t), Worn = t == ach.Title,
                }).ToList();
            }
            page.StreamQuests = streamerQuests.ForCharacter(c.Id).Select(q => Bounty(q, true)).ToList();
            return page;
        }

        private static QuestPageQuest Card(QuestInfo q, string command) => new QuestPageQuest
        {
            Kind = q.Kind,
            Text = QuestText(q),
            Progress = q.Progress,
            Amount = q.Amount,
            State = q.State,
            Reward = string.Format(CultureInfo.InvariantCulture, GameTexts.Get("UI_QUEST_REWARD", "{0} coins and {1} {2} exp"),
                Number(q.RewardCoins), Number((long)q.RewardExp), SkillName(q.RewardSkill)),
            Expires = q.Expires,
            Command = command,
        };

        private QuestPageChain Chain(QuestChainInfo ch, QuestInfo active)
        {
            var under = ch.State == 1 && active != null && active.Key == ch.Key;
            var piece = VillageDecorManager.Pieces.FirstOrDefault(p => p.Key == ch.RewardDecor);
            return new QuestPageChain
            {
                Key = ch.Key,
                Name = GameTexts.Get("UI_CHAIN_" + ch.Key, ch.Key),
                State = ch.State,
                Step = ch.Step,
                Steps = ch.Steps,
                Event = ch.Event,
                // under way: this step's words; not begun yet: the first step's, as a taste of the story
                Story = under ? GameTexts.Get("UI_CHAIN_" + ch.Key + "_" + (ch.Step + 1), null)
                    : ch.State == 0 || ch.State == 5 ? GameTexts.Get("UI_CHAIN_" + ch.Key + "_1", null) : null,
                Task = under ? QuestText(active) : null,
                Progress = under ? active.Progress : 0,
                Amount = under ? active.Amount : 0,
                Ending = ch.State == 2 ? GameTexts.Get("UI_CHAIN_" + ch.Key + "_END", null) : null,
                RewardTitle = TitleText(ch.RewardTitle),
                RewardDecor = piece?.Name,
                RewardDecorKey = piece?.Key,
                Command = ch.State == 0 ? "!quest begin " + ch.Key : null,
            };
        }

        private static QuestPageAchievement Achievement(AchievementInfo a)
        {
            var targets = a.Targets ?? Array.Empty<long>();
            var next = a.Tier < targets.Length ? targets[a.Tier] : 0;
            return new QuestPageAchievement
            {
                Key = a.Key,
                Name = GameTexts.Get("UI_ACHIEVEMENT_" + a.Key, a.Key),
                Tier = a.Tier,
                Progress = a.Progress,
                Next = next,
                Targets = targets,
                Hidden = a.Hidden,
                TierTitles = Enumerable.Range(1, 3).Select(t => TitleText(a.Key + "_" + t)).ToArray(),
            };
        }

        // ---- The streamer ---------------------------------------------------------------------------------------

        private QuestPageStreamer StreamerOf(Guid userId, VillageLog log)
        {
            var (best, rank) = hero.BestOf(userId);
            return new QuestPageStreamer
            {
                Stories = log.Stories.Select(Story).ToList(),
                Unlocks = log.Unlocks.Select(u => GameTexts.Get("UI_UNLOCK_" + u, null) ?? TitleText(u.StartsWith("title_") ? u.Substring(6) : u)).Where(t => !string.IsNullOrEmpty(t)).ToList(),
                Quests = streamerQuests.HistoryOf(userId).Select(q => Bounty(q, false)).ToList(),
                LoyaltyLeft = streamerQuests.LoyaltyLeft(userId),
                LoyaltyWeekly = StreamerQuestManager.WeeklyLoyaltyBudget,
                HeroWaves = best?.Waves ?? 0,
                HeroKills = best?.Kills ?? 0,
                HeroSeconds = best?.Seconds ?? 0,
                HeroRank = rank,
            };
        }

        private static QuestPageStory Story(VillageStoryInfo s)
        {
            var under = s.State == 1;
            return new QuestPageStory
            {
                Key = s.Key,
                Name = GameTexts.Get("UI_STORY_" + s.Key, s.Key),
                State = s.State,
                Step = s.Step,
                Steps = s.Steps,
                Hook = GameTexts.Get("UI_STORY_" + s.Key + "_HOOK", null),
                Line = under ? GameTexts.Get("UI_STORY_" + s.Key + "_" + (s.Step + 1), null) : null,
                Task = under ? StoryTask(s) : null,
                Progress = under ? s.Progress : 0,
                Amount = under && s.Kind != "talk" && s.Kind != "choice" ? s.Amount : 0,
                Helpers = s.Helpers?.Select(h => h.Name).Where(n => !string.IsNullOrEmpty(n)).Take(5).ToArray() ?? Array.Empty<string>(),
            };
        }

        private static QuestPageBounty Bounty(StreamerQuestInfo q, bool forCharacter) => new QuestPageBounty
        {
            Name = q.Name,
            Description = q.Description,
            Goal = q.Kind == 2,
            Task = StreamerTask(q),
            Amount = q.Amount,
            Progress = forCharacter && q.Kind == 1 ? q.YourProgress : q.Progress,
            Winners = q.Winners,
            Claimed = q.Claimed,
            Helpers = q.Helpers,
            State = q.State,
            Ends = q.Ends,
            YouClaimed = forCharacter && q.YouClaimed,
            Reward = BountyReward(q),
        };

        // ---- Words, as the game has them (QuestChat.Text, StoryLog.Task) ----------------------------------------

        private static string QuestText(QuestInfo q)
        {
            var amount = Number(q.Amount);
            if (q.Kind == 2)
            {
                var seasonal = GameTexts.Get("UI_QUEST_" + q.Key, null);
                if (seasonal != null) return Format(seasonal, amount);
            }
            switch (q.Objective)
            {
                case "gather":
                    switch ((Skill)q.Skill)
                    {
                        case Skill.Woodcutting: return Format(GameTexts.Get("UI_QUEST_GATHER_WOOD", "Chop {0} wood"), amount);
                        case Skill.Mining: return Format(GameTexts.Get("UI_QUEST_GATHER_ORE", "Mine {0} ore"), amount);
                        case Skill.Fishing: return Format(GameTexts.Get("UI_QUEST_GATHER_FISH", "Catch {0} fish"), amount);
                        case Skill.Farming: return Format(GameTexts.Get("UI_QUEST_GATHER_WHEAT", "Harvest {0} wheat"), amount);
                        default: return Format(GameTexts.Get("UI_QUEST_GATHER_HERBS", "Gather {0} times"), amount);
                    }
                case "exp": return Format(GameTexts.Get("UI_QUEST_EXP", "Gain {0} {1} exp"), amount, SkillName(q.Skill));
                case "craft": return Format(GameTexts.Get("UI_QUEST_CRAFT", "Craft {0} items"), amount);
                case "cook": return Format(GameTexts.Get("UI_QUEST_COOK", "Cook {0} meals"), amount);
                case "brew": return Format(GameTexts.Get("UI_QUEST_BREW", "Brew {0} potions"), amount);
                case "kill": return Format(GameTexts.Get("UI_QUEST_KILL", "Defeat {0} enemies"), amount);
                case "raid": return Format(GameTexts.Get(q.Amount == 1 ? "UI_QUEST_RAID_ONE" : "UI_QUEST_RAID", "Win {0} raids"), amount);
                case "dungeon": return Format(GameTexts.Get(q.Amount == 1 ? "UI_QUEST_DUNGEON_ONE" : "UI_QUEST_DUNGEON", "Clear {0} dungeons"), amount);
                case "hero_wave": return Format(GameTexts.Get("UI_QUEST_HERO_WAVES", "Hold {0} waves"), amount);
                case "deliver": return Format(GameTexts.Get("UI_QUEST_DELIVER", "Bring {0} {1}"), amount, q.Target);
                case "visit": return Format(GameTexts.Get("UI_QUEST_VISIT", "Sail to {0} and say !quest there"), q.Target);
                case "answer": return Format(GameTexts.Get("UI_QUEST_ANSWER", "Answer with !quest answer 1 to {0}"), q.Options);
                default: return q.Key;
            }
        }

        private static string StoryTask(VillageStoryInfo s)
        {
            var amount = Number(s.Amount);
            switch (s.Kind)
            {
                case "talk": return Format(GameTexts.Get("UI_STORY_TASK_TALK", "Talk to {0}"), GameTexts.Get("UI_STORY_" + s.Key + "_NPC", s.Key));
                case "deliver": return Format(GameTexts.Get("UI_STORY_TASK_DELIVER", "Bring {0} {1}"), amount, s.Target);
                case "find": return Format(GameTexts.Get("UI_STORY_" + s.Key + "_FIND", "Find {0}"), amount);
                case "viewers": return Format(GameTexts.Get("UI_STORY_TASK_VIEWERS", "Have {0} players in the stream at once"), amount);
                case "choice": return GameTexts.Get("UI_STORY_TASK_CHOICE", "The streamer makes the choice");
                case "kill": return Format(GameTexts.Get("UI_QUEST_KILL", "Defeat {0} enemies"), amount);
                case "raid": return Format(GameTexts.Get(s.Amount == 1 ? "UI_QUEST_RAID_ONE" : "UI_QUEST_RAID", "Win {0} raids"), amount);
                case "dungeon": return Format(GameTexts.Get(s.Amount == 1 ? "UI_QUEST_DUNGEON_ONE" : "UI_QUEST_DUNGEON", "Clear {0} dungeons"), amount);
                case "gather": return Format(GameTexts.Get("UI_QUEST_GATHER_HERBS", "Gather {0} times"), amount);
                default: return s.Kind;
            }
        }

        private static string StreamerTask(StreamerQuestInfo q)
        {
            var amount = Number(q.Amount);
            switch (q.Objective)
            {
                case "levels": return Format(GameTexts.Get("UI_STREAM_QUEST_LEVELS", "Gain {0} levels"), amount);
                case "craft":
                case "cook":
                case "brew": return Format(GameTexts.Get("UI_STREAM_QUEST_MAKE", "Craft, cook or brew {0} items"), amount);
                default: return QuestText(new QuestInfo { Objective = q.Objective, Skill = q.Skill, Amount = q.Amount, Key = q.Objective });
            }
        }

        private static string BountyReward(StreamerQuestInfo q)
        {
            var parts = new List<string>();
            if (q.RewardCoins > 0) parts.Add(Number(q.RewardCoins) + " coins");
            if (q.RewardItemAmount > 0 && !string.IsNullOrEmpty(q.RewardItemName)) parts.Add((q.RewardItemAmount > 1 ? Number(q.RewardItemAmount) + " " : "") + q.RewardItemName);
            if (q.RewardLoyalty > 0) parts.Add(Number(q.RewardLoyalty) + " loyalty points");
            return string.Join(", ", parts);
        }

        private static string TitleText(string key) => string.IsNullOrEmpty(key) ? null : GameTexts.Get("UI_TITLE_" + key, key.Replace('_', ' '));

        private static string SkillName(int index) =>
            Enum.IsDefined(typeof(Skill), index) ? GameTexts.Get("UI_SKILL_" + ((Skill)index).ToString().ToUpperInvariant(), ((Skill)index).ToString()) : "";

        private static string Format(string format, params object[] args)
        {
            try { return string.Format(CultureInfo.InvariantCulture, format, args); }
            catch (FormatException) { return format; }
        }

        private static string Number(long value) => value.ToString("N0", CultureInfo.InvariantCulture);
    }

    // ---- What the page shows -----------------------------------------------------------------------------------

    public class QuestPageData
    {
        public List<QuestPageCharacter> Characters { get; } = new List<QuestPageCharacter>();
        public QuestPageStreamer Streamer { get; set; }
        public List<string> Errors { get; } = new List<string>();
    }

    public class QuestPageCharacter
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public int Index { get; set; }
        /// <summary>The stream it plays in now, or null.</summary>
        public string StreamName { get; set; }
        public int Streak { get; set; }
        public long DailiesCompleted { get; set; }
        /// <summary>The title worn, in words, or null.</summary>
        public string Title { get; set; }
        public List<QuestPageQuest> Taken { get; set; } = new List<QuestPageQuest>();
        public List<QuestPageQuest> Offers { get; set; } = new List<QuestPageQuest>();
        public QuestPageQuest Weekly { get; set; }
        public QuestPageQuest Seasonal { get; set; }
        public List<QuestPageChain> Chains { get; set; } = new List<QuestPageChain>();
        public List<QuestPageAchievement> Achievements { get; set; } = new List<QuestPageAchievement>();
        public List<QuestPageTitle> Titles { get; set; } = new List<QuestPageTitle>();
        public List<QuestPageBounty> StreamQuests { get; set; } = new List<QuestPageBounty>();
    }

    public class QuestPageQuest
    {
        /// <summary>0 daily, 1 weekly, 2 seasonal.</summary>
        public int Kind { get; set; }
        public string Text { get; set; }
        public long Progress { get; set; }
        public long Amount { get; set; }
        /// <summary>0 offered, 1 taken, 2 finished, 3 expired, 4 given up.</summary>
        public int State { get; set; }
        public string Reward { get; set; }
        public DateTime Expires { get; set; }
        /// <summary>The chat command that takes it, while it is on offer.</summary>
        public string Command { get; set; }
        public double Share => Amount > 0 ? Math.Clamp((double)Progress / Amount, 0, 1) : 0;
    }

    public class QuestPageChain
    {
        public string Key { get; set; }
        public string Name { get; set; }
        /// <summary>0 can be begun, 1 under way, 2 finished, 5 only during its event.</summary>
        public int State { get; set; }
        public int Step { get; set; }
        public int Steps { get; set; }
        public string Event { get; set; }
        /// <summary>Under way: the quest giver's words for this step, the task, and its progress.</summary>
        public string Story { get; set; }
        public string Task { get; set; }
        public long Progress { get; set; }
        public long Amount { get; set; }
        /// <summary>Finished: how it ended.</summary>
        public string Ending { get; set; }
        public string RewardTitle { get; set; }
        public string RewardDecor { get; set; }
        public string RewardDecorKey { get; set; }
        public string Command { get; set; }
    }

    public class QuestPageAchievement
    {
        public string Key { get; set; }
        public string Name { get; set; }
        /// <summary>0 none, 1 bronze, 2 silver, 3 gold.</summary>
        public int Tier { get; set; }
        public long Progress { get; set; }
        /// <summary>The next tier's target, or 0 at gold.</summary>
        public long Next { get; set; }
        public long[] Targets { get; set; }
        public bool Hidden { get; set; }
        /// <summary>The title each tier gives.</summary>
        public string[] TierTitles { get; set; }
        public double Share => Next > 0 ? Math.Clamp((double)Progress / Next, 0, 1) : 1;
    }

    public class QuestPageTitle
    {
        public int Number { get; set; }
        public string Text { get; set; }
        public bool Worn { get; set; }
    }

    public class QuestPageBounty
    {
        public string Name { get; set; }
        public string Description { get; set; }
        /// <summary>A stream goal (everyone together); else a bounty (each on their own).</summary>
        public bool Goal { get; set; }
        public string Task { get; set; }
        public long Amount { get; set; }
        public long Progress { get; set; }
        public int Winners { get; set; }
        public int Claimed { get; set; }
        public int Helpers { get; set; }
        /// <summary>1 running, 2 done, 3 out of time, 4 stopped, 5 every reward claimed.</summary>
        public int State { get; set; }
        public DateTime Ends { get; set; }
        public bool YouClaimed { get; set; }
        public string Reward { get; set; }
        public double Share => Amount > 0 ? Math.Clamp((double)Progress / Amount, 0, 1) : 0;
    }

    public class QuestPageStreamer
    {
        public List<QuestPageStory> Stories { get; set; } = new List<QuestPageStory>();
        public List<string> Unlocks { get; set; } = new List<string>();
        public List<QuestPageBounty> Quests { get; set; } = new List<QuestPageBounty>();
        public long LoyaltyLeft { get; set; }
        public long LoyaltyWeekly { get; set; }
        public int HeroWaves { get; set; }
        public int HeroKills { get; set; }
        public int HeroSeconds { get; set; }
        public int HeroRank { get; set; }
    }

    public class QuestPageStory
    {
        public string Key { get; set; }
        public string Name { get; set; }
        /// <summary>0 not begun, 1 under way, 2 finished, 4 given up.</summary>
        public int State { get; set; }
        public int Step { get; set; }
        public int Steps { get; set; }
        public string Hook { get; set; }
        public string Line { get; set; }
        public string Task { get; set; }
        public long Progress { get; set; }
        public long Amount { get; set; }
        public string[] Helpers { get; set; }
        public double Share => Amount > 0 ? Math.Clamp((double)Progress / Amount, 0, 1) : 0;
    }
}

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
            Key = q.Key,
            Text = QuestText(q),
            Picture = QuestPicture(q.Objective, q.Skill),
            Coins = q.RewardCoins,
            Exp = (long)q.RewardExp,
            ExpSkill = SkillName(q.RewardSkill),
            ExpSkillIcon = SkillIcon(q.RewardSkill),
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
                Town = string.IsNullOrEmpty(ch.Town) ? null : RavenNest.Combat.Tower.Town(ch.Town)?.Name ?? ch.Town,
                RewardItem = ch.RewardItem,
                RewardItemIcon = string.IsNullOrEmpty(ch.RewardItem) ? null : "/imgs/items/" + ch.RewardItem.Replace(" ", "-").Replace("'", "").ToLowerInvariant() + ".png",
                Command = ch.State == 0 ? "!quest begin " + ch.Key : null,
                Picture = Picture("chain-" + ch.Key.ToLowerInvariant()),
                Banner = Picture("chain-" + ch.Key.ToLowerInvariant() + "-wide", "jpg"),
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
                Picture = AchievementPicture(a.Key),
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
                Unlocks = log.Unlocks.Select(Unlock).Where(u => !string.IsNullOrEmpty(u.Text)).ToList(),
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
                Ending = s.State == 2 ? GameTexts.Get("UI_STORY_" + s.Key + "_END", null) : null,
                Picture = Picture("story-" + s.Key.ToLowerInvariant()),
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
            Picture = QuestPicture(q.Objective, q.Skill),
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

        private static QuestPageUnlock Unlock(string key)
        {
            var title = key.StartsWith("title_");
            var text = GameTexts.Get("UI_UNLOCK_" + key, null) ?? TitleText(title ? key.Substring(6) : key);
            // where it came from: the islands and perks show their story's picture, titles a crown
            string picture = null;
            if (key.Contains("mangrove") || key.Contains("bananas")) picture = Picture("story-bananas");
            else if (key.Contains("ravebridge")) picture = Picture("story-ravebridge");
            else if (key.Contains("runestone")) picture = Picture("story-runestone");
            return new QuestPageUnlock { Key = key, Text = text, Picture = picture, Title = title };
        }

        // ---- Pictures: the game's quest window pictures (QuestArt), copied to wwwroot/imgs/quests --------------

        private static string Picture(string name, string extension = "png") => "/imgs/quests/" + name + "." + extension;

        /// <summary>The picture of a quest's objective, the same as the game's quest window (QuestArt.ObjectivePicture).</summary>
        private static string QuestPicture(string objective, int skill)
        {
            switch (objective)
            {
                case "gather":
                    switch ((Skill)skill)
                    {
                        case Skill.Woodcutting: return Picture("quest-wood");
                        case Skill.Mining: return Picture("quest-ore");
                        case Skill.Fishing: return Picture("quest-fish");
                        case Skill.Farming: return Picture("quest-wheat");
                        default: return Picture("quest-herbs");
                    }
                case "kill": return Picture("quest-kill");
                case "make":
                case "craft":
                case "cook":
                case "brew": return Picture("quest-make");
                case "exp":
                case "levels": return Picture("quest-levels");
                case "raid": return Picture("quest-raid");
                case "dungeon": return Picture("quest-dungeon");
                case "hero_wave": return Picture("quest-hero");
                default: return Picture("empty-board");
            }
        }

        /// <summary>An achievement's picture: the quest or item picture of the same thing where there is one, so a thing looks the same everywhere.</summary>
        private static string AchievementPicture(string key)
        {
            switch (key)
            {
                case "angler": return Picture("quest-fish");
                case "woodcutter": return Picture("quest-wood");
                case "miner": return Picture("quest-ore");
                case "farmer": return Picture("quest-wheat");
                case "forager": return Picture("quest-herbs");
                case "slayer": return Picture("quest-kill");
                case "raider": return Picture("quest-raid");
                case "delver": return Picture("quest-dungeon");
                case "crafter": return Picture("quest-make");
                case "cook": return Picture("rb-sauce");
                case "quester": return Picture("empty-board");
                case "ferryman": return "/imgs/items/rowboat.png";
                default: return Picture("ach-" + key);
            }
        }

        /// <summary>The website's skill icon (imgs/icons/skills), or null.</summary>
        private static string SkillIcon(int index)
        {
            if (!Enum.IsDefined(typeof(Skill), index)) return null;
            switch ((Skill)index)
            {
                case Skill.Attack: return "/imgs/icons/skills/Attack.png";
                case Skill.Defense: return "/imgs/icons/skills/Defense.png";
                case Skill.Strength: return "/imgs/icons/skills/Strength.png";
                case Skill.Health: return "/imgs/icons/skills/health.png";
                case Skill.Woodcutting: return "/imgs/icons/skills/woodcutting.png";
                case Skill.Fishing: return "/imgs/icons/skills/Fishing.png";
                case Skill.Mining: return "/imgs/icons/skills/Mining.png";
                case Skill.Crafting: return "/imgs/icons/skills/Crafting.png";
                case Skill.Cooking: return "/imgs/icons/skills/Cooking.png";
                case Skill.Farming: return "/imgs/icons/skills/farming.png";
                case Skill.Slayer: return "/imgs/icons/skills/slayer.png";
                case Skill.Magic: return "/imgs/icons/skills/Magic.png";
                case Skill.Ranged: return "/imgs/icons/skills/ranged.png";
                case Skill.Sailing: return "/imgs/icons/skills/sailing.png";
                case Skill.Healing: return "/imgs/icons/skills/healing.png";
                default: return null;
            }
        }

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
        public string Key { get; set; }
        public string Text { get; set; }
        public string Picture { get; set; }
        public long Coins { get; set; }
        public long Exp { get; set; }
        public string ExpSkill { get; set; }
        public string ExpSkillIcon { get; set; }
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
        /// <summary>The tower town the chain is begun in (its people give it), or null for one begun anywhere.</summary>
        public string Town { get; set; }
        public string RewardItem { get; set; }
        public string RewardItemIcon { get; set; }
        public string Command { get; set; }
        public string Picture { get; set; }
        /// <summary>A wide picture for the chain's page.</summary>
        public string Banner { get; set; }
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
        public string Picture { get; set; }
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
        public string Picture { get; set; }
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
        public List<QuestPageUnlock> Unlocks { get; set; } = new List<QuestPageUnlock>();
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
        /// <summary>Finished: how it ended.</summary>
        public string Ending { get; set; }
        public string Picture { get; set; }
        public string Line { get; set; }
        public string Task { get; set; }
        public long Progress { get; set; }
        public long Amount { get; set; }
        public string[] Helpers { get; set; }
        public double Share => Amount > 0 ? Math.Clamp((double)Progress / Amount, 0, 1) : 0;
    }

    public class QuestPageUnlock
    {
        public string Key { get; set; }
        public string Text { get; set; }
        /// <summary>The picture of the story it came from, or null (a title shows a crown).</summary>
        public string Picture { get; set; }
        public bool Title { get; set; }
    }
}

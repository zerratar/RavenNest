using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Data;
using RavenNest.DataModels;
using RavenNest.Models;
using Skill = RavenNest.Models.Skill;
using Skills = RavenNest.DataModels.Skills;

namespace RavenNest.BusinessLogic.Game
{
    /// <summary>
    /// Where the server's activity reaches the quests: gathering, experience, crafting, raids, dungeons and kills
    /// call these as they happen, on whatever thread they run. <see cref="QuestManager"/> listens.
    /// </summary>
    /// <remarks>
    /// Static so the task processors (made with <c>new T()</c>, without services) can report too, as
    /// <c>GameEventRouter.Handler</c> does for events.
    /// </remarks>
    public static class QuestTracker
    {
        internal static QuestManager Listener;
        internal static StreamQuestManager StreamListener;

        private static void Report(Character character, string objective, int skill, long amount)
        {
            Listener?.Progress(character, objective, skill, amount);
            StreamListener?.Progress(character, objective, skill, amount);
        }

        /// <summary>One gathering round (a resource for the village) of a skill.</summary>
        public static void Gathered(Character character, Skill skill) => Report(character, QuestManager.Gather, (int)skill, 1);

        public static void Experience(Character character, int skillIndex, double gained)
        {
            if (gained > 0) Listener?.Progress(character, QuestManager.Exp, skillIndex, (long)Math.Floor(gained));
        }

        /// <summary>Levels gained in any skill (stream quests count them).</summary>
        public static void LevelsGained(Character character, int skillIndex, int levels)
        {
            if (levels > 0) StreamListener?.Progress(character, StreamQuestManager.Levels, -1, levels);
        }

        /// <summary>Items made: crafted (Crafting), cooked (Cooking) or brewed (Alchemy).</summary>
        public static void Made(Character character, Skill skill, long amount)
        {
            var objective = skill == Skill.Cooking ? QuestManager.Cook : skill == Skill.Alchemy ? QuestManager.Brew : QuestManager.Craft;
            if (amount > 0) Report(character, objective, -1, amount);
        }

        public static void RaidWon(Character character) => Report(character, QuestManager.Raid, -1, 1);
        public static void DungeonCleared(Character character) => Report(character, QuestManager.Dungeon, -1, 1);

        public static void Kills(Character character, long count)
        {
            if (count > 0) Report(character, QuestManager.Kill, -1, count);
        }
    }

    /// <summary>
    /// Personal quests (phase 1 of docs/quests-design.md in the game repository): each day three offers made for the
    /// character's levels and one weekly quest; up to three daily quests taken at a time; progress counted on the
    /// server from what the character does (<see cref="QuestTracker"/>); a finished quest pays coins at once and tells
    /// the game (QuestCompleted), which adds the experience. A daily streak grows the daily rewards.
    /// </summary>
    /// <remarks>
    /// The catalog is here, as the decor price list is: templates that make a quest for a character, with numbers
    /// that fit them. An offer is made once per day (or week) and kept, so asking twice shows the same three.
    /// </remarks>
    public class QuestManager
    {
        public const string Gather = "gather";
        public const string Exp = "exp";
        public const string Craft = "craft";
        public const string Cook = "cook";
        public const string Brew = "brew";
        public const string Kill = "kill";
        public const string Raid = "raid";
        public const string Dungeon = "dungeon";

        public const int Daily = 0, Weekly = 1;
        public const int Offered = 0, Active = 1, Done = 2, Expired = 3, Abandoned = 4;
        public const int MaxActiveDaily = 3;
        public const int DailyOffers = 3;
        private const double StreakBonusPerDay = 0.1;
        private const int StreakBonusDays = 5;

        private static readonly Skill[] GatherSkills = { Skill.Woodcutting, Skill.Mining, Skill.Fishing, Skill.Farming, Skill.Gathering };

        /// <summary>A kind of quest: whether it suits a character, and the quest it makes for them.</summary>
        private sealed class Template
        {
            public string Key;
            public int Kind;
            public string Objective;
            public Func<Skills, Random, bool> Fits = (s, r) => true;
            public Func<Skills, Random, (int skill, long amount, int rewardSkill)> Make;
        }

        // First numbers, set 2026-10-25, to be balanced. A gathering round comes every 10 seconds, so 120 rounds is
        // about 20 minutes of training.
        private static readonly Template[] Catalog =
        {
            Gathering("gather", Daily, 90, 150),
            new Template { Key = "exp", Kind = Daily, Objective = Exp, Make = (s, r) => ExpQuest(s, r, 0.25) },
            new Template { Key = "craft", Kind = Daily, Objective = Craft, Fits = (s, r) => s.CraftingLevel >= 10, Make = (s, r) => ((int)Skill.Crafting, r.Next(3, 9), (int)Skill.Crafting) },
            new Template { Key = "cook", Kind = Daily, Objective = Cook, Make = (s, r) => ((int)Skill.Cooking, r.Next(10, 31), (int)Skill.Cooking) },
            new Template { Key = "brew", Kind = Daily, Objective = Brew, Fits = (s, r) => s.AlchemyLevel >= 10, Make = (s, r) => ((int)Skill.Alchemy, r.Next(5, 16), (int)Skill.Alchemy) },
            new Template { Key = "kill", Kind = Daily, Objective = Kill, Make = (s, r) => (-1, r.Next(4, 13) * 10, (int)Skill.Slayer) },
            new Template { Key = "raid", Kind = Daily, Objective = Raid, Make = (s, r) => (-1, 1, (int)Skill.Slayer) },
            new Template { Key = "dungeon", Kind = Daily, Objective = Dungeon, Make = (s, r) => (-1, 1, (int)Skill.Slayer) },

            Gathering("weekly_gather", Weekly, 1200, 1800),
            new Template { Key = "weekly_exp", Kind = Weekly, Objective = Exp, Make = (s, r) => ExpQuest(s, r, 1.5) },
            new Template { Key = "weekly_kill", Kind = Weekly, Objective = Kill, Make = (s, r) => (-1, r.Next(8, 13) * 100, (int)Skill.Slayer) },
            new Template { Key = "weekly_raid", Kind = Weekly, Objective = Raid, Make = (s, r) => (-1, 5, (int)Skill.Slayer) },
            new Template { Key = "weekly_dungeon", Kind = Weekly, Objective = Dungeon, Make = (s, r) => (-1, 3, (int)Skill.Slayer) },
        };

        private static Template Gathering(string key, int kind, int min, int max) => new Template
        {
            Key = key, Kind = kind, Objective = Gather,
            Make = (s, r) => { var skill = (int)GatherSkills[r.Next(GatherSkills.Length)]; return (skill, r.Next(min, max + 1) / 10 * 10, skill); }
        };

        /// <summary>Experience in one of the character's best five skills: a share of what their next level takes.</summary>
        private static (int skill, long amount, int rewardSkill) ExpQuest(Skills s, Random r, double shareOfLevel)
        {
            var best = s.GetSkills().Where(x => x.Index != (int)Skill.Sailing).OrderByDescending(x => x.Level).Take(5).ToList();
            var pick = best[r.Next(best.Count)];
            var amount = Math.Max(100, (long)(GameMath.ExperienceForLevel(Math.Min(pick.Level + 1, GameMath.MaxLevel)) * shareOfLevel));
            return (pick.Index, amount, pick.Index);
        }

        private readonly GameData gameData;
        private readonly ILogger<QuestManager> logger;
        private readonly object mutex = new object();

        public QuestManager(GameData gameData, ILogger<QuestManager> logger)
        {
            this.gameData = gameData;
            this.logger = logger;
            QuestTracker.Listener = this;
        }

        // ---- What the game asks ----------------------------------------------------------------------

        /// <summary>The character's quests now: making today's offers and this week's quest the first time.</summary>
        public QuestList GetQuests(SessionToken token, Guid characterId)
        {
            var character = CharacterOf(token, characterId);
            if (character == null) return null;
            lock (mutex)
            {
                var quests = Refresh(character);
                var stats = gameData.GetCharacterQuestStats(character.Id);
                var day = DayOf(DateTime.UtcNow);
                return new QuestList
                {
                    Active = quests.Where(q => q.Kind == Daily && q.State == Active).OrderBy(q => q.Accepted).Select(Info).ToArray(),
                    Offers = quests.Where(q => q.Kind == Daily && q.State == Offered && q.Period == day).OrderBy(q => q.Slot).Select(Info).ToArray(),
                    Weekly = quests.Where(q => q.Kind == Weekly && q.Period == WeekOf(DateTime.UtcNow)).Select(Info).FirstOrDefault(),
                    Streak = CurrentStreak(stats),
                    DailiesCompleted = stats?.DailiesCompleted ?? 0,
                };
            }
        }

        /// <summary>Takes one of today's offers (slot 0 to 2), or the weekly quest (slot -1).</summary>
        public QuestActionResult Accept(SessionToken token, Guid characterId, int slot)
        {
            var character = CharacterOf(token, characterId);
            if (character == null) return new QuestActionResult { Error = "notinsession" };
            lock (mutex)
            {
                var quests = Refresh(character);
                var quest = slot < 0
                    ? quests.FirstOrDefault(q => q.Kind == Weekly && q.Period == WeekOf(DateTime.UtcNow))
                    : quests.FirstOrDefault(q => q.Kind == Daily && q.Period == DayOf(DateTime.UtcNow) && q.Slot == slot);
                if (quest == null) return new QuestActionResult { Error = "unknown" };
                if (quest.State == Active) return new QuestActionResult { Error = "taken", Quest = Info(quest) };
                if (quest.State == Done) return new QuestActionResult { Error = "done", Quest = Info(quest) };
                if (quest.State != Offered) return new QuestActionResult { Error = "expired", Quest = Info(quest) };
                if (quest.Kind == Daily && quests.Count(q => q.Kind == Daily && q.State == Active) >= MaxActiveDaily)
                    return new QuestActionResult { Error = "full", Quest = Info(quest) };

                quest.State = Active;
                quest.Accepted = DateTime.UtcNow;
                return new QuestActionResult { Ok = true, Quest = Info(quest) };
            }
        }

        /// <summary>Gives up a taken quest (an offer taken back is gone for the day).</summary>
        public QuestActionResult Abandon(SessionToken token, Guid characterId, Guid questId)
        {
            var character = CharacterOf(token, characterId);
            if (character == null) return new QuestActionResult { Error = "notinsession" };
            lock (mutex)
            {
                var quest = Refresh(character).FirstOrDefault(q => q.Id == questId);
                if (quest == null || quest.State != Active) return new QuestActionResult { Error = "unknown" };
                quest.State = Abandoned;
                return new QuestActionResult { Ok = true, Quest = Info(quest) };
            }
        }

        // ---- Progress -------------------------------------------------------------------------------------

        internal void Progress(Character character, string objective, int skill, long amount)
        {
            if (character == null || amount <= 0) return;
            try
            {
                var quests = gameData.GetCharacterQuests(character.Id);
                if (quests.Count == 0) return;

                List<CharacterQuest> finished = null;
                lock (mutex)
                {
                    var now = DateTime.UtcNow;
                    foreach (var q in quests)
                    {
                        if (q.State != Active || q.Objective != objective || (q.Skill >= 0 && skill >= 0 && q.Skill != skill)) continue;
                        if (q.Expires <= now) { q.State = Expired; continue; }

                        q.Progress = Math.Min(q.Amount, q.Progress + amount);
                        if (q.Progress >= q.Amount)
                        {
                            q.State = Done;
                            q.Completed = now;
                            (finished ??= new List<CharacterQuest>()).Add(q);
                        }
                    }

                    if (finished != null)
                    {
                        foreach (var q in finished) Reward(character, q);
                    }
                }
            }
            catch (Exception exc)
            {
                logger.LogError("Quest progress failed for " + character.Name + ": " + exc);
            }
        }

        /// <summary>Pays the coins, counts the streak and tells the game, which adds the experience and says so.</summary>
        private void Reward(Character character, CharacterQuest quest)
        {
            var stats = StatsOf(character);
            var today = DayOf(DateTime.UtcNow);
            if (quest.Kind == Daily)
            {
                if (stats.LastDailyDay != today)
                {
                    stats.DailyStreak = stats.LastDailyDay == DayOf(DateTime.UtcNow.AddDays(-1)) ? stats.DailyStreak + 1 : 1;
                    stats.LastDailyDay = today;
                }
                stats.DailiesCompleted++;
            }
            else
            {
                stats.WeekliesCompleted++;
            }

            var resources = gameData.GetResources(character);
            if (resources != null) resources.Coins += quest.RewardCoins;
            gameData.Touch();

            var session = gameData.GetSessionByCharacterId(character.Id);
            if (session != null)
            {
                gameData.EnqueueGameEvent(gameData.CreateSessionEvent(GameEventType.QuestCompleted, session,
                    new QuestCompleted { CharacterId = character.Id, Quest = Info(quest), Streak = stats.DailyStreak }));
            }
        }

        // ---- Offers ----------------------------------------------------------------------------------------

        /// <summary>
        /// The character's quests, with yesterday's (or last week's) left behind as expired and today's offers and this
        /// week's quest made if there are none yet.
        /// </summary>
        private IReadOnlyList<CharacterQuest> Refresh(Character character)
        {
            var now = DateTime.UtcNow;
            var quests = gameData.GetCharacterQuests(character.Id);
            foreach (var q in quests.ToList())
            {
                if ((q.State == Offered || q.State == Active) && q.Expires <= now) q.State = Expired;

                // four rows a day add up: what ended over a week ago goes (the streak and the counts are kept apart)
                if (q.State != Active && q.Expires < now.AddDays(-7)) gameData.Remove(q);
            }

            var day = DayOf(now);
            var week = WeekOf(now);
            quests = gameData.GetCharacterQuests(character.Id);
            var hasToday = quests.Any(q => q.Kind == Daily && q.Period == day);
            var hasWeek = quests.Any(q => q.Kind == Weekly && q.Period == week);
            if (hasToday && hasWeek) return quests;

            var skills = gameData.GetCharacterSkills(character.SkillsId);
            if (skills == null) return quests;

            if (!hasToday)
            {
                var random = new Random(Seed(character.Id, day));
                var expires = now.Date.AddDays(1);
                var picked = Catalog.Where(t => t.Kind == Daily && t.Fits(skills, random)).OrderBy(t => random.Next()).Take(DailyOffers).ToList();
                for (var i = 0; i < picked.Count; ++i)
                {
                    gameData.Add(Make(character, skills, picked[i], random, day, i, expires));
                }
            }

            if (!hasWeek)
            {
                var random = new Random(Seed(character.Id, week));
                var daysToMonday = ((int)DayOfWeek.Monday - (int)now.DayOfWeek + 7) % 7;
                var expires = now.Date.AddDays(daysToMonday == 0 ? 7 : daysToMonday);
                var weekly = Catalog.Where(t => t.Kind == Weekly && t.Fits(skills, random)).OrderBy(t => random.Next()).First();
                gameData.Add(Make(character, skills, weekly, random, week, 0, expires));
            }

            return gameData.GetCharacterQuests(character.Id);
        }

        private CharacterQuest Make(Character character, Skills skills, Template template, Random random, string period, int slot, DateTime expires)
        {
            var (skill, amount, rewardSkill) = template.Make(skills, random);

            // coins and experience grow with the character: the average level, and a share of the reward skill's
            // next level; a weekly quest pays about six dailies
            var average = skills.GetSkills().Average(x => x.Level);
            var weekly = template.Kind == Weekly ? 6.0 : 1.0;
            var rewardLevel = skills.GetSkill(rewardSkill)?.Level ?? 1;
            return new CharacterQuest
            {
                Id = Guid.NewGuid(),
                CharacterId = character.Id,
                Key = template.Key,
                Kind = template.Kind,
                Period = period,
                Slot = slot,
                Objective = template.Objective,
                Skill = skill,
                Amount = amount,
                State = Offered,
                RewardCoins = (long)Math.Round((300 + average * 15) * weekly / 10) * 10,
                RewardExp = Math.Round(GameMath.ExperienceForLevel(Math.Min(rewardLevel + 1, GameMath.MaxLevel)) * 0.15 * weekly),
                RewardSkill = rewardSkill,
                Offered = DateTime.UtcNow,
                Expires = expires,
            };
        }

        // ---- Helpers ---------------------------------------------------------------------------------------

        private QuestInfo Info(CharacterQuest q)
        {
            // the daily streak adds to what a daily pays out, shown before it is finished as well
            var bonus = 1.0;
            if (q.Kind == Daily && q.State != Done)
            {
                var stats = gameData.GetCharacterQuestStats(q.CharacterId);
                bonus += StreakBonusPerDay * Math.Min(StreakBonusDays, CurrentStreak(stats));
            }

            return new QuestInfo
            {
                Id = q.Id,
                Key = q.Key,
                Kind = q.Kind,
                Slot = q.Slot,
                Objective = q.Objective,
                Skill = q.Skill,
                Amount = q.Amount,
                Progress = q.Progress,
                State = q.State,
                RewardCoins = (long)(q.RewardCoins * bonus),
                RewardExp = Math.Round(q.RewardExp * bonus),
                RewardSkill = q.RewardSkill,
                Expires = q.Expires,
            };
        }

        /// <summary>The streak that still counts: a day missed ends it.</summary>
        private static int CurrentStreak(CharacterQuestStats stats)
        {
            if (stats == null) return 0;
            var now = DateTime.UtcNow;
            return stats.LastDailyDay == DayOf(now) || stats.LastDailyDay == DayOf(now.AddDays(-1)) ? stats.DailyStreak : 0;
        }

        private CharacterQuestStats StatsOf(Character character)
        {
            var stats = gameData.GetCharacterQuestStats(character.Id);
            if (stats != null) return stats;
            stats = new CharacterQuestStats { Id = Guid.NewGuid(), CharacterId = character.Id };
            gameData.Add(stats);
            return stats;
        }

        /// <summary>A character of this stream's session: quests are asked for by the game the character plays in.</summary>
        private Character CharacterOf(SessionToken token, Guid characterId)
        {
            var character = gameData.GetCharacter(characterId);
            if (character == null || token == null || character.UserIdLock != token.UserId) return null;
            return character;
        }

        private static string DayOf(DateTime utc) => utc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        private static string WeekOf(DateTime utc) =>
            ISOWeek.GetYear(utc).ToString(CultureInfo.InvariantCulture) + "-W" + ISOWeek.GetWeekOfYear(utc).ToString("00", CultureInfo.InvariantCulture);

        /// <summary>The same for a character and a day in every server run (string hashes change between runs).</summary>
        private static int Seed(Guid characterId, string period)
        {
            var hash = characterId.GetHashCode();
            foreach (var c in period) hash = hash * 31 + c;
            return hash;
        }
    }
}

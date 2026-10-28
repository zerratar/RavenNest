using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using RavenNest.BusinessLogic.Data;
using RavenNest.DataModels;
using RavenNest.Models;
using Skill = RavenNest.Models.Skill;

namespace RavenNest.BusinessLogic.Game
{
    /// <summary>
    /// Achievements (phase 3 of docs/quests-design.md in the game repository): lifetime milestones counted by the same
    /// tracker as quests, with nothing to accept. Each has up to three tiers (bronze, silver, gold) at growing targets,
    /// and each tier earns a title; finished quest chains earn titles too. A character wears one title on its name tag
    /// (!title). A few hidden ones are for the silly things.
    /// </summary>
    /// <remarks>
    /// The catalog is here, as the quest catalog is. Title keys are the achievement's key and tier ("woodcutter_2") or
    /// the chain's key ("cat"); the game words them. Counting starts when this ships: nothing before it is known.
    /// </remarks>
    public class AchievementManager
    {
        private enum Mode { Add, Highest }

        private sealed class Definition
        {
            public string Key;
            public long[] Targets;
            public bool Hidden;
            public Mode Mode = Mode.Add;
        }

        // First targets, set 2026-10-25, to be balanced. A gathering round comes every 10 seconds per player.
        private static readonly Definition[] Catalog =
        {
            Tiers("woodcutter", 1_000, 10_000, 100_000),
            Tiers("miner", 1_000, 10_000, 100_000),
            Tiers("angler", 1_000, 10_000, 100_000),
            Tiers("farmer", 1_000, 10_000, 100_000),
            Tiers("forager", 1_000, 10_000, 100_000),
            Tiers("slayer", 1_000, 10_000, 100_000),
            Tiers("raider", 10, 100, 1_000),
            Tiers("delver", 10, 100, 1_000),
            Tiers("crafter", 100, 1_000, 10_000),
            Tiers("cook", 100, 1_000, 10_000),
            Tiers("alchemist", 100, 1_000, 10_000),
            Tiers("quester", 10, 100, 1_000),
            Tiers("helper", 5, 50, 500),
            Tiers("generous", 1, 10, 100),
            // guests carried in an own boat, a whole trip each (BoatManager): the Ferryman, then the Sea Captain
            new Definition { Key = "ferryman", Targets = new long[] { 50, 500 } },
            new Definition { Key = "allround", Targets = new long[] { 50, 200, 999 }, Mode = Mode.Highest },
            new Definition { Key = "storyteller", Targets = new long[] { 1, 3, 5 }, Mode = Mode.Highest },
            new Definition { Key = "wrong", Targets = new long[] { 5 }, Hidden = true },
            new Definition { Key = "quitter", Targets = new long[] { 10 }, Hidden = true },
        };

        private static Definition Tiers(string key, long bronze, long silver, long gold) =>
            new Definition { Key = key, Targets = new[] { bronze, silver, gold } };

        private readonly GameData gameData;
        private readonly ILogger<AchievementManager> logger;
        private readonly object mutex = new object();

        public AchievementManager(GameData gameData, ILogger<AchievementManager> logger)
        {
            this.gameData = gameData;
            this.logger = logger;
            QuestTracker.Achievements = this;
        }

        // ---- What the tracker reports --------------------------------------------------------------------

        /// <summary>Something a character did, as the quest tracker saw it.</summary>
        internal void Activity(Character character, string objective, int skill, long amount)
        {
            switch (objective)
            {
                case QuestManager.Gather:
                    switch ((Skill)skill)
                    {
                        case Skill.Woodcutting: Add(character, "woodcutter", amount); break;
                        case Skill.Mining: Add(character, "miner", amount); break;
                        case Skill.Fishing: Add(character, "angler", amount); break;
                        case Skill.Farming: Add(character, "farmer", amount); break;
                        case Skill.Gathering: Add(character, "forager", amount); break;
                    }
                    break;
                case QuestManager.Kill: Add(character, "slayer", amount); break;
                case QuestManager.Raid: Add(character, "raider", amount); break;
                case QuestManager.Dungeon: Add(character, "delver", amount); break;
                case QuestManager.Craft: Add(character, "crafter", amount); break;
                case QuestManager.Cook: Add(character, "cook", amount); break;
                case QuestManager.Brew: Add(character, "alchemist", amount); break;
            }
        }

        /// <summary>The character's lowest skill level, for the all-rounder.</summary>
        internal void LevelsChanged(Character character)
        {
            var skills = character != null ? gameData.GetCharacterSkills(character.SkillsId) : null;
            if (skills == null) return;
            Set(character, "allround", skills.GetSkills().Min(x => x.Level));
        }

        internal void Add(Character character, string key, long amount)
        {
            if (character == null || amount <= 0) return;
            Count(character, key, amount, false);
        }

        internal void Set(Character character, string key, long value)
        {
            if (character == null) return;
            Count(character, key, value, true);
        }

        private void Count(Character character, string key, long amount, bool highest)
        {
            var definition = Catalog.FirstOrDefault(d => d.Key == key);
            if (definition == null) return;
            try
            {
                int reached;
                lock (mutex)
                {
                    var row = RowOf(character, key);
                    var progress = highest ? Math.Max(row.Progress, amount) : row.Progress + amount;
                    if (progress == row.Progress) return;
                    row.Progress = progress;

                    var tier = 0;
                    while (tier < definition.Targets.Length && progress >= definition.Targets[tier]) tier++;
                    if (tier <= row.Tier) return;
                    row.Tier = tier;
                    row.Achieved = DateTime.UtcNow;
                    reached = tier;
                }

                gameData.Touch();
                var session = gameData.GetSessionByCharacterId(character.Id);
                if (session != null)
                {
                    gameData.EnqueueGameEvent(gameData.CreateSessionEvent(GameEventType.AchievementReached, session,
                        new AchievementReached { CharacterId = character.Id, Key = key, Tier = reached, Title = TitleOf(key, reached) }));
                }
            }
            catch (Exception exc)
            {
                logger.LogError("Achievement " + key + " failed for " + character.Name + ": " + exc);
            }
        }

        // ---- What the game asks ----------------------------------------------------------------------

        public AchievementList GetAchievements(SessionToken token, Guid characterId)
        {
            var character = CharacterOf(token, characterId);
            if (character == null) return null;
            LevelsChanged(character);
            var rows = gameData.GetCharacterAchievements(character.Id);
            var list = new List<AchievementInfo>();
            foreach (var d in Catalog)
            {
                var row = rows.FirstOrDefault(r => r.Key == d.Key);
                if (d.Hidden && (row == null || row.Tier == 0)) continue; // hidden until reached
                list.Add(new AchievementInfo { Key = d.Key, Progress = row?.Progress ?? 0, Tier = row?.Tier ?? 0, Targets = d.Targets, Hidden = d.Hidden });
            }

            return new AchievementList
            {
                Achievements = list.ToArray(),
                Titles = EarnedTitles(character).ToArray(),
                Title = gameData.GetCharacterQuestStats(character.Id)?.Title,
            };
        }

        /// <summary>Wears an earned title, or none with null, "" or "none".</summary>
        public TitleResult SetTitle(SessionToken token, Guid characterId, string title)
        {
            var character = CharacterOf(token, characterId);
            if (character == null) return new TitleResult { Error = "notinsession" };
            if (string.IsNullOrEmpty(title) || title == "none") title = null;
            else if (!EarnedTitles(character).Contains(title)) return new TitleResult { Error = "notearned" };

            lock (mutex)
            {
                var stats = gameData.GetCharacterQuestStats(character.Id);
                if (stats == null)
                {
                    stats = new CharacterQuestStats { Id = Guid.NewGuid(), CharacterId = character.Id };
                    gameData.Add(stats);
                }
                stats.Title = title;
            }
            return new TitleResult { Ok = true, Title = title };
        }

        /// <summary>The titles worn by the characters in the stream now.</summary>
        public SessionTitles GetSessionTitles(SessionToken token)
        {
            var session = token != null ? gameData.GetSession(token.SessionId) : null;
            if (session == null) return null;
            var ids = new List<Guid>();
            var titles = new List<string>();
            foreach (var c in gameData.GetActiveSessionCharacters(session))
            {
                var title = gameData.GetCharacterQuestStats(c.Id)?.Title;
                if (string.IsNullOrEmpty(title)) continue;
                ids.Add(c.Id);
                titles.Add(title);
            }
            return new SessionTitles { CharacterIds = ids.ToArray(), Titles = titles.ToArray() };
        }

        // ---- Helpers ---------------------------------------------------------------------------------------

        /// <summary>Every tier's title reached, then the finished chains' titles.</summary>
        private IEnumerable<string> EarnedTitles(Character character)
        {
            foreach (var row in gameData.GetCharacterAchievements(character.Id))
            {
                for (var tier = 1; tier <= row.Tier; ++tier) yield return TitleOf(row.Key, tier);
            }

            foreach (var q in gameData.GetCharacterQuests(character.Id))
            {
                if (q.Kind == QuestManager.Chain && q.State == QuestManager.Done) yield return q.Key;
            }

            // the streamer's own characters wear the titles their story lines earned
            foreach (var unlock in gameData.GetStreamerUnlocks(character.UserId))
            {
                if (unlock.Key.StartsWith("title_", StringComparison.Ordinal)) yield return unlock.Key.Substring("title_".Length);
            }
        }

        private static string TitleOf(string key, int tier) => key + "_" + tier;

        private CharacterAchievement RowOf(Character character, string key)
        {
            var row = gameData.GetCharacterAchievements(character.Id).FirstOrDefault(r => r.Key == key);
            if (row != null) return row;
            row = new CharacterAchievement { Id = Guid.NewGuid(), CharacterId = character.Id, Key = key, Achieved = DateTime.UtcNow };
            gameData.Add(row);
            return row;
        }

        private Character CharacterOf(SessionToken token, Guid characterId)
        {
            var character = gameData.GetCharacter(characterId);
            if (character == null || token == null || character.UserIdLock != token.UserId) return null;
            return character;
        }
    }
}

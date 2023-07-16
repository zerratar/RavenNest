using RavenNest.BusinessLogic.Data;
using RavenNest.DataModels;
using System;
using System.Collections.Generic;
using System.Linq;

using StringTimeDictionary = System.Collections.Concurrent.ConcurrentDictionary<string, System.DateTime>;
using GuidTimeDictionary = System.Collections.Concurrent.ConcurrentDictionary<System.Guid, System.DateTime>;
using StateDictionary = System.Collections.Concurrent.ConcurrentDictionary<System.Guid, RavenNest.BusinessLogic.Game.Processors.Tasks.ClanSkillState>;
using RavenNest.BusinessLogic.Net;
using RavenNest.BusinessLogic.Providers;

namespace RavenNest.BusinessLogic.Game.Processors.Tasks
{
    public class ClanSkillUpdate
    {
        public DateTime LastUpdate { get; set; }
        public int Level { get; set; }
        public long Experience { get; set; }
    }

    public class ClanProcessor : PlayerTaskProcessor
    {
        private static readonly GuidTimeDictionary clanExpUpdate = new GuidTimeDictionary();
        private static readonly StringTimeDictionary clanExpAnnouncement = new StringTimeDictionary();

        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, ClanSkillUpdate> skillUpdate
            = new System.Collections.Concurrent.ConcurrentDictionary<string, ClanSkillUpdate>();

        private readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(20);

        private readonly TimeSpan SkillExpUpdateInterval = TimeSpan.FromSeconds(10);
        //private static readonly Version ClientVersion_ClanLevel = new Version(0, 7, 1);
        public override void Process(
             GameData gameData,
             PlayerInventoryProvider inventoryProvider,
             GameSession session,
             Character character,
             CharacterState state)
        {

            var membership = gameData.GetClanMembership(character.Id);
            if (membership == null)
                return;

            var clan = gameData.GetClan(membership.ClanId);
            if (clan == null)
                return;

            UpdateClanExperience(gameData, session, clan);
            UpdateClanSkillsExperience(gameData, session, character, state, clan);
        }

        private void UpdateClanSkillsExperience(
            GameData gameData,
            GameSession session,
            Character character,
            CharacterState state,
            Clan clan)
        {
            var clanSkills = gameData.GetClanSkills(clan.Id);
            if (clanSkills.Count == 0) // we only have 1 clan skill.
            {
                // check which skills the clan is eligble for and add those in.
                EnsureClanSkills(gameData, clan, clanSkills);
                clanSkills = gameData.GetClanSkills(clan.Id);
            }

            // if character is training a clan skill
            // we will increase the exp gain of that skill.
            // we then push info to the clients every N'th second regarding the exp update.
            // but only if the exp has actually changed.

            // if level up, send announcement. Otherwise send it every other second for the game session if exp is being updated
            // incase someone training the clan skill on a different stream.

            var enchanting = clanSkills.FirstOrDefault();
            var key = session.Id + "_" + enchanting.Id;
            var now = DateTime.UtcNow;

            var currentLevel = enchanting.Level;
            var currentExp = (long)enchanting.Experience;
            if (!skillUpdate.TryGetValue(key, out var lastAnnouncement))
                skillUpdate[key] = lastAnnouncement = new ClanSkillUpdate();

            if ((now - lastAnnouncement.LastUpdate > SkillExpUpdateInterval && lastAnnouncement.Experience != currentExp)
                || enchanting.Level != currentLevel)
            {
                gameData.EnqueueGameEvent(gameData.CreateSessionEvent(RavenNest.Models.GameEventType.ClanSkillLevelChanged,
                    session,
                    new ClanSkillLevelChanged
                    {
                        ClanId = clan.Id,
                        SkillId = enchanting.Id,
                        Experience = currentExp,
                        Level = currentLevel,
                    }));

                lastAnnouncement.Experience = currentExp;
                lastAnnouncement.Level = currentLevel;
            }
        }
        private void UpdateClanExperience(GameData gameData, GameSession session, Clan clan)
        {
            var now = DateTime.UtcNow;
            var elapsed = TimeSpan.Zero;

            if (clanExpUpdate.TryGetValue(clan.Id, out var lastUpdate))
                elapsed = now - lastUpdate;

            if (clan.Level == 0)
                clan.Level = 1;

            var exp = GameMath.GetClanExperience(clan.Level, elapsed);

            clan.Experience += exp;

            var nextLevel = GameMath.ExperienceForLevel(clan.Level + 1);
            var oldLevel = clan.Level;

            while (clan.Experience >= nextLevel)
            {
                clan.Experience -= nextLevel;
                ++clan.Level;
                nextLevel = GameMath.ExperienceForLevel(clan.Level + 1);
            }

            clanExpUpdate[clan.Id] = now;

            var announcementKey = session.Id + "_" + clan.Id; // Use session ID and not clan ID as we need to send to all open clients and not just once per clan.

            if (!clanExpAnnouncement.TryGetValue(announcementKey, out var lastAnnouncement))
                clanExpAnnouncement[announcementKey] = now;

            if (now - lastAnnouncement > UpdateInterval)
            {
                gameData.EnqueueGameEvent(gameData.CreateSessionEvent(RavenNest.Models.GameEventType.ClanLevelChanged, session, new ClanLevelChanged
                {
                    ClanId = clan.Id,
                    Experience = (long)clan.Experience,
                    Level = clan.Level,
                    LevelDelta = clan.Level - oldLevel,

                }));
                clanExpAnnouncement[announcementKey] = now;
            }
        }
        private static void EnsureClanSkills(GameData gameData, Clan clan, IReadOnlyList<ClanSkill> clanSkills)
        {
            foreach (var s in gameData
                .GetSkills()
                .Where(x => x.RequiredClanLevel <= clan.Level && !clanSkills.Any(y => y.SkillId == x.Id)))
            {
                var newSkill = new ClanSkill
                {
                    Id = Guid.NewGuid(),
                    ClanId = clan.Id,
                    Experience = 0,
                    Level = 1,
                    SkillId = s.Id
                };

                gameData.Add(newSkill);
            }
        }

        private static ClanSkill GetTrainingSkill(GameData gameData, CharacterState state, List<ClanSkill> clanSkills)
        {
            ClanSkill trainingSkill = null;
            foreach (var cs in clanSkills)
            {
                var skill = gameData.GetSkill(cs.SkillId);
                var task = state.Task;
                var taskArg = state.TaskArgument;

                if (string.IsNullOrEmpty(task))
                    continue;

                if (task.Equals(skill.Name, StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrEmpty(taskArg) && taskArg.Equals(skill.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    trainingSkill = cs;
                    break;
                }
            }

            return trainingSkill;
        }
        private double GetExperience(int skillLevel, double seconds)
        {
            return (seconds * (((1d + (skillLevel / 100d) + (skillLevel / 75d)) * (double)Math.Pow(2, (double)(skillLevel / 20d)) / 20d) + 1d)) + (seconds * 8.3d);
        }
    }

    public class ClanSkillState
    {
        public ClanSkill Skill { get; set; }
        public DateTime StartTime { get; set; }
        public TimeSpan Duration { get; set; }
    }
}

﻿using RavenNest.BusinessLogic.Data;
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
    public class ClanProcessor : PlayerTaskProcessor
    {
        private static readonly GuidTimeDictionary clanExpUpdate = new GuidTimeDictionary();
        private static readonly StringTimeDictionary clanExpAnnouncement = new StringTimeDictionary();
        private static readonly StringTimeDictionary clanSkillAnnouncement = new StringTimeDictionary();
        private static readonly StateDictionary trainingState = new StateDictionary();

        private readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(2);

        //private static readonly Version ClientVersion_ClanLevel = new Version(0, 7, 1);
        public override void Process(
             IIntegrityChecker integrityChecker,
             IGameData gameData,
             IPlayerInventoryProvider inventoryProvider,
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
            IGameData gameData,
            GameSession session,
            Character character,
            CharacterState state,
            Clan clan)
        {
            var clanSkills = gameData.GetClanSkills(clan.Id).ToList();

            // check which skills the clan is eligble for and add those in.
            EnsureClanSkills(gameData, clan, clanSkills);

            // if character is training a clan skill
            // we will increase the exp gain of that skill.
            // we then push info to the clients every N'th second regarding the exp update.
            // but only if the exp has actually changed.

            if (!trainingState.TryGetValue(character.Id, out var ts))
                trainingState[character.Id] = (ts = new ClanSkillState());

            var trainingSkill = GetTrainingSkill(gameData, state, clanSkills);
            if (trainingSkill == null)
            {
                ts.Skill = null;
                ts.Duration = TimeSpan.Zero;
                ts.StartTime = DateTime.MaxValue;
                return;
            }
            else if (ts.Skill != trainingSkill)
            {
                ts.Skill = trainingSkill;
                ts.Duration = TimeSpan.Zero;
                ts.StartTime = DateTime.UtcNow;
            }

            var dictKey = session.Id + "_" + trainingSkill.Id;
            var now = DateTime.UtcNow;
            var elapsed = now - ts.StartTime;
            ts.Duration = ts.Duration.Add(elapsed);

            var exp = GetExperience(trainingSkill.Level, (decimal)elapsed.TotalSeconds);

            ts.Skill.Experience += exp;
            var gainedLevels = 0;
            var nextLevel = GameMath.ExperienceForLevel(trainingSkill.Level + 1);
            while (exp >= nextLevel)
            {
                exp -= nextLevel;
                nextLevel = GameMath.ExperienceForLevel(trainingSkill.Level + 1);
                ts.Skill.Level = ts.Skill.Level + 1;
                ++gainedLevels;
            }

            // if level up, send announcement. Otherwise send it every other second for the game session if exp is being updated
            // incase someone training the clan skill on a different stream.

            if (!clanSkillAnnouncement.TryGetValue(dictKey, out var lastAnnouncement))
                clanSkillAnnouncement[dictKey] = now;

            if (now - lastAnnouncement > UpdateInterval)
            {
                gameData.Add(gameData.CreateSessionEvent(GameEventType.ClanLevelChanged, session, new ClanSkillLevelChanged
                {
                    ClanId = clan.Id,
                    SkillId = trainingSkill.Id,
                    Experience = (long)ts.Skill.Experience,
                    Level = ts.Skill.Level,
                    LevelDelta = gainedLevels,
                }));
                clanSkillAnnouncement[dictKey] = now;
            }
        }
        private void UpdateClanExperience(IGameData gameData, GameSession session, Clan clan)
        {
            var now = DateTime.UtcNow;
            var elapsed = TimeSpan.Zero;

            if (clanExpUpdate.TryGetValue(clan.Id, out var lastUpdate))
                elapsed = now - lastUpdate;

            if (clan.Level == 0)
                clan.Level = 1;

            clan.Experience += (decimal)elapsed.TotalSeconds;

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
                gameData.Add(gameData.CreateSessionEvent(GameEventType.ClanLevelChanged, session, new ClanLevelChanged
                {
                    ClanId = clan.Id,
                    Experience = (long)clan.Experience,
                    Level = clan.Level,
                    LevelDelta = clan.Level - oldLevel,
                }));
                clanExpAnnouncement[announcementKey] = now;
            }
        }
        private static void EnsureClanSkills(IGameData gameData, Clan clan, List<ClanSkill> clanSkills)
        {
            foreach (var s in gameData.GetSkills().Where(x => x.RequiredClanLevel <= clan.Level))
            {
                if (!clanSkills.Any(x => x.SkillId == s.Id))
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
                    clanSkills.Add(newSkill);
                }
            }
        }
        private static ClanSkill GetTrainingSkill(IGameData gameData, CharacterState state, List<ClanSkill> clanSkills)
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
        private decimal GetExperience(int skillLevel, decimal seconds)
        {
            return (seconds * (((1m + (decimal)(skillLevel / 100f) + (decimal)(skillLevel / 75f)) * (decimal)Math.Pow(2, (double)(skillLevel / 20d)) / 20m) + 1m)) + (seconds * 8.3m);
        }
    }

    public class ClanSkillState
    {
        public ClanSkill Skill { get; set; }
        public DateTime StartTime { get; set; }
        public TimeSpan Duration { get; set; }
    }
}

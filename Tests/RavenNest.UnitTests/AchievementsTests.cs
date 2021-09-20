using Microsoft.VisualStudio.TestTools.UnitTesting;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Game;
using RavenNest.Models;
using Shinobytes.Ravenfall.Core.RuleEngine;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RavenNest.UnitTests
{

    [TestClass]
    public class AchievementsTests
    {

        [TestMethod]
        public void UsingGambit_CreateAchievement_UnlockAchievement()
        {
            var achievementUnlocked = false;
            var engineGenerator = new GambitGenerator();
            var engine = engineGenerator.CreateEngine<PlayerAchievementFact>();
            var fact = new PlayerAchievementFact();
            fact.Player = new Player();
            fact.Player.Statistics = new Statistics();
            fact.Player.Statistics.RaidsWon = 1;


            engine.AddRule(engine.CreateRule("Boss Slayer",
                engine.CreateCondition(x => x.Player.Statistics.RaidsWon >= 1 && x.Player.Statistics.DungeonsWon >= 1),
                engine.CreateAction(x =>
                {
                    //x.PlayerManager.UnlockAchievement(x.Rule.Name, )
                    achievementUnlocked = true;
                })));

            var a = engine.ProcessRules(fact);


            fact.Player.Statistics.DungeonsWon = 1;
            var b = engine.ProcessRules(fact);

            Assert.IsTrue(achievementUnlocked);
        }


        [TestMethod]
        public void CreateAchievement_ReportStatus_UnlockAchievement()
        {
            var userId = Guid.NewGuid();
            var characterId = Guid.NewGuid();

            var achievements = new AchievementManager();
            var progress = achievements.Trigger(userId, characterId, GameTrigger.RaidBossKilled);
            Assert.AreEqual(progress, 0.5f);

            progress = achievements.Trigger(userId, characterId, GameTrigger.DungeonCleared);
            Assert.AreEqual(progress, 1.0);
        }
    }

    public class PlayerAchievementFact
    {
        public Player Player { get; set; }
        public GameSession Session { get; set; }
        public IPlayerManager PlayerManager { get; set; }
        public IGambitRule<PlayerAchievementFact> Rule { get; set; }
        public IGameData GameData { get; set; }
    }

    public enum GameTrigger
    {
        StreamCount,
        StreamedHours,
        PlayedHours,
        PlayedStreamCount,
        SimultanousPlayedStreamCount,
        AveragePlayersJoined,
        PlayersJoined,
        PlayersKilled,
        Died,
        EnemiesKilled,
        ItemsGifted,
        ItemsCrafted,
        ItemsVendored,
        ItemsSold,
        ItemsBought,
        ItemsRedeemed,
        RaidBossKilled,
        DungeonCleared,
        MinigameWon,
        MinigameLost,
    }

    public class AchievementManager
    {
        private Achievement achievement;
        private UserAchievement userAchievement;
        public AchievementManager()
        {
            userAchievement = new UserAchievement();
            userAchievement.Progress = new List<UserAchievementProgress>();

            achievement = new Achievement();
            achievement.Requirements = new List<AchievementRequirement>();

            var reqA = new AchievementRequirement();
            reqA.Trigger = GameTrigger.RaidBossKilled;
            reqA.Count = 1;
            achievement.Requirements.Add(reqA);

            var reqB = new AchievementRequirement();
            reqB.Trigger = GameTrigger.DungeonCleared;
            reqB.Count = 1;
            achievement.Requirements.Add(reqB);
        }

        internal float Trigger(
            Guid userId,
            Guid characterId,
            GameTrigger triggerType,
            string triggerData = null,
            int triggerCount = 1)
        {
            var requirement = achievement.Requirements.Where(x => x.Trigger == triggerType).ToList();
            if (requirement.Count == 0)
            {
                return -1; // no such requirement
            }

            // Grab the achievement for this requirement
            // if (requirement.AchievementId).. 

            // always go through all requirements to check if all
            // has been met.
            var requirementsMet = 0f;
            foreach (var req in achievement.Requirements)
            {
                var progress = userAchievement.Progress.FirstOrDefault(x => x.Trigger == req.Trigger && x.Data == req.Data);
                if (progress == null)
                {
                    progress = new UserAchievementProgress
                    {
                        Trigger = req.Trigger,
                        Data = req.Data
                    };
                    userAchievement.Progress.Add(progress);
                }

                if (progress.Count >= req.Count && progress.Data == req.Data)
                {
                    ++requirementsMet;
                    continue;
                }

                if (req.Trigger == triggerType && req.Data == triggerData)
                {
                    progress.Count += triggerCount;
                    if (progress.Count >= req.Count && progress.Data == req.Data)
                    {
                        ++requirementsMet;
                    }
                }
            }

            return requirementsMet / achievement.Requirements.Count;
        }
    }

    public class Achievement
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public IList<AchievementRequirement> Requirements { get; set; }
        public IList<AchievementReward> Rewards { get; set; }
    }

    public class AchievementReward
    {
        public Guid Id { get; set; }
        public Guid AchievementId { get; set; }
    }

    public class AchievementRequirement
    {
        public Guid Id { get; set; }
        public Guid AchievementId { get; set; }
        public GameTrigger Trigger { get; set; }
        public string Data { get; set; }
        public int Count { get; set; }
    }

    public class UserAchievement
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public Guid? CharacterId { get; set; }
        public Guid AchievementId { get; set; }
        public bool Completed { get; set; }
        public IList<UserAchievementProgress> Progress { get; set; }
    }

    public class UserAchievementProgress
    {
        public Guid Id { get; set; }
        public Guid UserAchievementId { get; set; }
        public GameTrigger Trigger { get; set; }
        public string Data { get; set; }
        public int Count { get; set; }
    }
}

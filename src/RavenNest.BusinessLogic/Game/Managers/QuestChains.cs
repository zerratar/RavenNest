using System.Linq;
using Skill = RavenNest.Models.Skill;

namespace RavenNest.BusinessLogic.Game
{
    /// <summary>
    /// The quest chains (phase 3 of docs/quests-design.md in the game repository): short stories of steps, each step
    /// unlocking the next. A step is a normal objective (counted by QuestTracker as the daily quests are), or one of the
    /// chain's own: deliver items (!quest deliver), visit an island (be there and say !quest), or answer a riddle
    /// (!quest answer 2). The game has the story lines, by the chain's key and the step's number; this has the numbers.
    /// A finished chain earns its title (the chain's key) and unlocks its decor piece for the village where it ended.
    /// </summary>
    public static class QuestChains
    {
        public sealed class Step
        {
            public string Objective;
            public int Skill = -1;
            public long Amount = 1;

            /// <summary>The item to deliver, or the island to visit.</summary>
            public string Target;

            /// <summary>An answer step: how many choices, and the right one (from 1), or 0 when any answer does.</summary>
            public int Options;
            public int Correct;
        }

        public sealed class Chain
        {
            public string Key;

            /// <summary>The seasonal event it can be begun in (VillageDecorManager.Events), or null for any time.</summary>
            public string Event;

            /// <summary>The skill the finishing experience goes to.</summary>
            public Skill RewardSkill;
            public Step[] Steps;
        }

        // Written 2026-10-25. Items are ones gathering drops at low levels (Raw Sprat, Logs, Iron Ore, ...).
        public static readonly Chain[] All =
        {
            // The barkeep's cat, Sir Whiskers, has run off again.
            new Chain
            {
                Key = "cat", RewardSkill = Skill.Slayer,
                Steps = new[]
                {
                    Deliver("Raw Sprat", 10),
                    Gather(Skill.Woodcutting, 60),
                    Answer(3, 1),
                    Visit("Kyo"),
                    Kill(40),
                },
            },

            // Old Finn's giant fish keeps stealing his bait.
            new Chain
            {
                Key = "fish", RewardSkill = Skill.Fishing,
                Steps = new[]
                {
                    Gather(Skill.Fishing, 80),
                    Deliver("Raw Shrimp", 15),
                    Visit("Heim"),
                    Answer(3, 3),
                    Deliver("Hemp", 20),
                    Gather(Skill.Fishing, 150),
                },
            },

            // A statue for the hero who founded the village, whom nobody remembers.
            new Chain
            {
                Key = "founder", RewardSkill = Skill.Crafting,
                Steps = new[]
                {
                    Gather(Skill.Mining, 150),
                    Deliver("Iron Ore", 50),
                    Deliver("Logs", 100),
                    new Step { Objective = QuestManager.Dungeon, Amount = 1 },
                    new Step { Objective = QuestManager.Craft, Skill = (int)Skill.Crafting, Amount = 10 },
                    Answer(3, 0),
                    new Step { Objective = QuestManager.Cook, Skill = (int)Skill.Cooking, Amount = 30 },
                },
            },

            // A shipwright on Away wants to build something better than the old ferries.
            new Chain
            {
                Key = "shipwright", RewardSkill = Skill.Sailing,
                Steps = new[]
                {
                    Visit("Away"),
                    Gather(Skill.Woodcutting, 200),
                    Deliver("Logs", 150),
                    Deliver("Iron Ore", 60),
                    Deliver("Resin", 40),
                    new Step { Objective = QuestManager.Raid, Amount = 1 },
                    Deliver("Coal", 30),
                },
            },

            // Halloween: something stirs in the pumpkin patch.
            new Chain
            {
                Key = "pumpkin", Event = "halloween", RewardSkill = Skill.Farming,
                Steps = new[]
                {
                    Gather(Skill.Farming, 60),
                    Kill(66),
                    Answer(3, 1),
                    Deliver("Mushroom", 13),
                    Visit("Ironhill"),
                },
            },
        };

        public static Chain Get(string key) => All.FirstOrDefault(c => c.Key == key);

        private static Step Deliver(string item, long amount) => new Step { Objective = QuestManager.Deliver, Target = item, Amount = amount };
        private static Step Visit(string island) => new Step { Objective = QuestManager.Visit, Target = island };
        private static Step Answer(int options, int correct) => new Step { Objective = QuestManager.Answer, Options = options, Correct = correct };
        private static Step Gather(Skill skill, long amount) => new Step { Objective = QuestManager.Gather, Skill = (int)skill, Amount = amount };
        private static Step Kill(long amount) => new Step { Objective = QuestManager.Kill, Amount = amount };
    }
}

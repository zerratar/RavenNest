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

            /// <summary>
            ///     The tower town whose people give it (RavenNest.Combat.Tower.Towns, "mossgate"), or null. The game begins
            ///     a town's chain only for a player who is in the town.
            /// </summary>
            public string Town;

            /// <summary>An item given (soulbound) when the chain is finished, by its name; null for none.</summary>
            public string RewardItem;
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

            // ---- Mossgate, the tower's first town (2026-11-08) ------------------------------------------------
            // Its people give one chain each; the monsters are the first band's (RavenNest.Combat.Tower). A talk step is
            // done in the town (!tower talk elm); a tower kill counts kills on any floor, of one kind when it names one.

            // Warden Elm keeps the tower gate and wants to know what is up there before more climbers go.
            new Chain
            {
                Key = "warden", Town = "mossgate", RewardSkill = Skill.Defense, RewardItem = "Wardens Thornband",
                Steps = new[]
                {
                    TowerKill(null, 60),
                    TowerKill("hall-goblin", 40),
                    Deliver("Iron Ore", 40),
                    TowerClear(1),
                    Talk("elm"),
                },
            },

            // Bramble the herbalist is out of everything, and the good stuff grows on the floors.
            new Chain
            {
                Key = "bramble", Town = "mossgate", RewardSkill = Skill.Alchemy, RewardItem = "Bramble Charm",
                Steps = new[]
                {
                    Deliver("Mushroom", 15),
                    TowerKill("hall-metalon-green", 30),
                    Answer(3, 2),
                    new Step { Objective = QuestManager.Brew, Amount = 10 },
                    Talk("bramble"),
                },
            },

            // Pip's bunny Nibbles ran up the tower. Pip is sure Nibbles is fine. Nibbles is not fine.
            new Chain
            {
                Key = "nibbles", Town = "mossgate", RewardSkill = Skill.Slayer, RewardItem = "Holy Hand Grenade",
                Steps = new[]
                {
                    TowerKill("hall-bunny", 30),
                    Answer(3, 2),
                    Deliver("Cabbage", 20),
                    TowerClear(1),
                    Talk("pip"),
                },
            },

            // Rimeholt (the tower's second town, 2026-11-10): Hilda's forge keeps the whole town alive.
            new Chain
            {
                Key = "embers", Town = "rimeholt", RewardSkill = Skill.Crafting, RewardItem = "Embersmith Band",
                Steps = new[]
                {
                    TowerKill(null, 80),
                    TowerKill("frost-raider", 40),
                    Deliver("Coal", 50),
                    TowerClear(1),
                    Talk("hilda"),
                },
            },

            // Old Tove has fished the same hole for sixty winters, and nothing bites any more.
            new Chain
            {
                Key = "tove", Town = "rimeholt", RewardSkill = Skill.Fishing, RewardItem = "Frostbeard Amulet",
                Steps = new[]
                {
                    Gather(Skill.Fishing, 60),
                    Answer(3, 2),
                    TowerKill("snow-hare", 30),
                    Deliver("Raw Salmon", 25),
                    Talk("tove"),
                },
            },

            // Ylva's duck flew up the tower. Everybody knows where it really is. Nobody tells her.
            new Chain
            {
                Key = "duck", Town = "rimeholt", RewardSkill = Skill.Ranged, RewardItem = "Rubber Duck",
                Steps = new[]
                {
                    TowerKill("quackling", 30),
                    Answer(3, 1),
                    Deliver("Bread", 20),
                    TowerClear(1),
                    Talk("ylva"),
                },
            },

            // The barkeep found a speckled egg in the pantry, warm as fresh bread. It is not a hen's. (2026-11-09)
            new Chain
            {
                Key = "speckled", RewardSkill = Skill.Taming, RewardItem = "Dragon Spark Egg",
                Steps = new[]
                {
                    Deliver("Eggs", 12),
                    Gather(Skill.Farming, 50),
                    Kill(40),
                    Answer(3, 0),
                    Deliver("Wheat", 30),
                    Visit("Ironhill"),
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
        private static Step TowerKill(string kind, long amount) => new Step { Objective = QuestManager.TowerKill, Target = kind, Amount = amount };
        private static Step TowerClear(long amount) => new Step { Objective = QuestManager.TowerClear, Amount = amount };
        private static Step Talk(string npc) => new Step { Objective = QuestManager.Talk, Target = npc };
        private static Step Kill(long amount) => new Step { Objective = QuestManager.Kill, Amount = amount };
    }
}

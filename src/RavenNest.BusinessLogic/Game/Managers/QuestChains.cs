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

            /// <summary>
            ///     An experience step that fits every level: this share of the skill's next level, worked out when the
            ///     step begins (QuestManager.SetStep), in place of <see cref="Amount"/>. 0 for a fixed amount.
            /// </summary>
            public double Share;

            /// <summary>What the step asks in its place when it cannot be done: a skill at the highest level gains no experience.</summary>
            public Step Else;
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

            /// <summary>
            ///     The chain that must be finished first (its key), or null: a sequel is locked until then. Everyone who
            ///     finished the first chains before the sequels came has them open at once.
            /// </summary>
            public string Requires;

            /// <summary>A long chain whose later steps ask for high levels or big amounts: it pays twice a chain's coins and experience.</summary>
            public bool Grand;
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

            // Galewharf (the tower's third town, 2026-11-19): Captain Brisa's ship lost its sails to the sky pirates.
            new Chain
            {
                Key = "windlass", Town = "galewharf", RewardSkill = Skill.Sailing, RewardItem = "Windlass Band",
                Steps = new[]
                {
                    TowerKill(null, 100),
                    TowerKill("sky-pirate", 45),
                    Deliver("Sailcloth", 30),
                    TowerClear(1),
                    Talk("brisa"),
                },
            },

            // Tinker Volt wants to bottle a storm. He needs copper, a sharp mind and a lot of nerve. Mostly yours.
            new Chain
            {
                Key = "bottled", Town = "galewharf", RewardSkill = Skill.Crafting, RewardItem = "Bottled Lightning",
                Steps = new[]
                {
                    Gather(Skill.Mining, 60),
                    Answer(3, 2),
                    TowerKill("storm-wisp", 35),
                    Deliver("Bronze Bar", 25),
                    Talk("volt"),
                },
            },

            // Tessa's sheep floated off on a cloud. She can still hear him. Everyone can still hear him.
            new Chain
            {
                Key = "woolly", Town = "galewharf", RewardSkill = Skill.Farming, RewardItem = "Woolly's Bell",
                Steps = new[]
                {
                    TowerKill("squall-duckling", 30),
                    Answer(3, 3),
                    Deliver("Wheat", 30),
                    TowerClear(1),
                    Talk("tessa"),
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

            // ---- More stories (v0.9.7, 2026-11-20) --------------------------------------------------------------
            // Everyone had finished the first chains. Twelve more, spread over what players do; the grand ones end in
            // steps for high levels (items that drop late, big amounts) and pay double. Sequels are locked until their
            // first chain is done. Delivery amounts mind the drops' cooldowns (GameData.EnsureResourceDropRates): a
            // step is an hour or two of gathering for a character who has nothing put by.

            // Sir Whiskers has run off again, and this time he has a ship. (After the barkeep's lost cat.)
            new Chain
            {
                Key = "whiskers", Requires = "cat", RewardSkill = Skill.Slayer,
                Steps = new[]
                {
                    Deliver("Raw Salmon", 12),
                    Visit("Away"),
                    Kill(150),
                    Answer(3, 2),
                    Raid(2),
                    Deliver("Milk", 10),
                },
            },

            // Old Finn's prize sprat is gone from its plinth, and whatever took it is much bigger. (After the one that got away.)
            new Chain
            {
                Key = "bigfish", Requires = "fish", RewardSkill = Skill.Fishing, Grand = true,
                Steps = new[]
                {
                    Gather(Skill.Fishing, 300),
                    Deliver("Raw Lobster", 6),
                    Visit("Heim"),
                    Answer(3, 1),
                    Deliver("Raw Swordfish", 4),
                    Gather(Skill.Fishing, 600),
                    Deliver("Raw Octopus", 2),
                },
            },

            // The dwarves of Ironhill dug a new shaft, and something down there knocks back. (After the village founder.)
            new Chain
            {
                Key = "knock", Requires = "founder", RewardSkill = Skill.Mining, Grand = true,
                Steps = new[]
                {
                    Visit("Ironhill"),
                    Gather(Skill.Mining, 300),
                    Deliver("Coal", 80),
                    Deliver("Mithril Ore", 15),
                    Answer(3, 3),
                    Dungeon(2),
                    Deliver("Rune Ore", 12),
                    Deliver("Dragon Ore", 6),
                },
            },

            // Big Bjorn hit the old oak and it said ow. Out loud. Now it has demands.
            new Chain
            {
                Key = "oak", RewardSkill = Skill.Woodcutting, Grand = true,
                Steps = new[]
                {
                    Gather(Skill.Woodcutting, 200),
                    Answer(3, 0),
                    Deliver("Glowbark Logs", 25),
                    Deliver("Resin", 40),
                    Deliver("Ironbark Logs", 12),
                    Gather(Skill.Woodcutting, 500),
                    Deliver("Dragonwood Logs", 6),
                },
            },

            // A food critic has come to the tavern, and the barkeep is sweating.
            new Chain
            {
                Key = "critic", RewardSkill = Skill.Cooking,
                Steps = new[]
                {
                    Cook(40),
                    Deliver("Bread", 25),
                    Answer(3, 3),
                    Deliver("Salmon", 10),
                    Cook(150),
                    Deliver("Roasted Chicken", 6),
                    Deliver("Apple Pie", 4),
                },
            },

            // The alchemist has had hiccups for nine days and can cure anything but that.
            new Chain
            {
                Key = "hiccup", RewardSkill = Skill.Alchemy,
                Steps = new[]
                {
                    Brew(15),
                    Deliver("Yarrow", 30),
                    Deliver("Lavender", 12),
                    Answer(3, 2),
                    Kill(120),
                    Brew(50),
                    Deliver("Chamomile", 8),
                },
            },

            // A very tall knight in a very long coat orders armor. He is three goblins. Nobody says so.
            new Chain
            {
                Key = "tallknight", RewardSkill = Skill.Crafting,
                Steps = new[]
                {
                    Craft(10),
                    Deliver("Iron Ore", 80),
                    Deliver("Bronze Bar", 30),
                    Answer(3, 0),
                    Kill(200),
                    Craft(40),
                    Deliver("Steel Bar", 25),
                },
            },

            // The kennel master teaches taming (battle pets); it ends with an egg that growls.
            new Chain
            {
                Key = "kennel", RewardSkill = Skill.Taming, RewardItem = "Wolf Pup Egg",
                Steps = new[]
                {
                    Tame(1),
                    Exp(Skill.Taming, 0.5, Kill(100)),
                    Deliver("Raw Shrimp", 30),
                    Answer(3, 3),
                    Tame(3),
                    Kill(200),
                    Exp(Skill.Taming, 1.0, Kill(300)),
                },
            },

            // A sack of mail washed up, forty years late, with a letter for every island. (After the shipwright.)
            new Chain
            {
                Key = "postman", Requires = "shipwright", RewardSkill = Skill.Sailing,
                Steps = new[]
                {
                    Visit("Away"),
                    Visit("Ironhill"),
                    Answer(3, 1),
                    Visit("Kyo"),
                    Kill(120),
                    Visit("Heim"),
                    Visit("Atria"),
                    Visit("Eldara"),
                },
            },

            // The three innkeepers of the tower's towns each pour the best drink in the tower. Someone has to judge.
            // No town gives it (it is begun anywhere); its talk steps are done in the towns, so it needs a stream that
            // has climbed to Galewharf.
            new Chain
            {
                Key = "taster", RewardSkill = Skill.Strength, Grand = true,
                Steps = new[]
                {
                    Talk("mossbeard"),
                    Deliver("Mushroom", 25),
                    TowerKill(null, 150),
                    Talk("gunnhild"),
                    TowerKill("frost-raider", 50),
                    Talk("hob"),
                    TowerKill("sky-pirate", 60),
                    Answer(3, 0),
                    Talk("mossbeard"),
                },
            },

            // A bard wants to write a ballad about you, and needs material. Lots of it.
            new Chain
            {
                Key = "ballad", RewardSkill = Skill.Attack, Grand = true,
                Steps = new[]
                {
                    Kill(100),
                    Raid(1),
                    Answer(3, 0),
                    Dungeon(2),
                    Kill(400),
                    Raid(5),
                    Dungeon(5),
                },
            },

            // A magpie stole the jeweler's ring and trades it back: shiny for shinier (the raw gems mining drops).
            new Chain
            {
                Key = "magpie", RewardSkill = Skill.Gathering,
                Steps = new[]
                {
                    Deliver("Sapphire", 5),
                    Deliver("Emerald", 5),
                    Answer(3, 2),
                    Deliver("Ruby", 5),
                    Gather(Skill.Gathering, 200),
                    Deliver("Topaz", 4),
                    Deliver("Diamond", 2),
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
        private static Step Raid(long amount) => new Step { Objective = QuestManager.Raid, Amount = amount };
        private static Step Dungeon(long amount) => new Step { Objective = QuestManager.Dungeon, Amount = amount };
        private static Step Craft(long amount) => new Step { Objective = QuestManager.Craft, Skill = (int)Skill.Crafting, Amount = amount };
        private static Step Cook(long amount) => new Step { Objective = QuestManager.Cook, Skill = (int)Skill.Cooking, Amount = amount };
        private static Step Brew(long amount) => new Step { Objective = QuestManager.Brew, Amount = amount };
        private static Step Tame(long amount) => new Step { Objective = QuestManager.Tame, Amount = amount };

        /// <summary>Experience in a skill, as a share of its next level; <paramref name="orElse"/> when the skill is at the highest level.</summary>
        private static Step Exp(Skill skill, double shareOfLevel, Step orElse) =>
            new Step { Objective = QuestManager.Exp, Skill = (int)skill, Share = shareOfLevel, Else = orElse };
    }
}

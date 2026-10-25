using System.Linq;
using Skill = RavenNest.Models.Skill;

namespace RavenNest.BusinessLogic.Game
{
    /// <summary>
    /// The streamer's story lines as data (phase 5 of docs/quests-design.md in the game repository). A step is one of:
    /// <list type="bullet">
    /// <item>talk: the streamer talks to the story's NPC (the quest window, or a click on the NPC in the world);</item>
    /// <item>deliver: the stream brings items to the village (!quest give, and the item gathering drops while the step
    /// runs goes there by itself);</item>
    /// <item>gather, kill, raid, dungeon: counted for the whole stream, as stream quests are;</item>
    /// <item>find: while the step runs, a gathering round of the skill sometimes finds the story's thing (a rune
    /// fragment, a wild banana), and the finder is named;</item>
    /// <item>viewers: so many players in the stream at once;</item>
    /// <item>choice: the streamer picks one of the options (and can let chat vote first).</item>
    /// </list>
    /// The game has the NPC's lines, by the story's key and the step's number; this has the numbers. Rewards: the
    /// story's decor piece (VillageDecorPiece.Quest is the story's key), its unlocks (an island, a title), a village
    /// perk (which may depend on a choice), and a share of coins for the viewers who helped.
    /// </summary>
    public static class VillageStories
    {
        public const string Talk = "talk", Deliver = "deliver", Find = "find", Viewers = "viewers", Choice = "choice";

        public sealed class Step
        {
            public string Kind;
            public int Skill = -1;
            public long Amount = 1;
            public string Target;
            public int Options;

            /// <summary>A find step: the chance a gathering round of the skill finds one.</summary>
            public double Chance;
        }

        public sealed class Story
        {
            public string Key;
            public Step[] Steps;

            /// <summary>Unlocked for the streamer when it is finished (island_mangrove, title_runebreaker).</summary>
            public string[] Unlocks;

            /// <summary>The village perk; with a choice step, "perk_runestone_" and the option chosen.</summary>
            public string Perk;
            public int PerkChoiceStep = -1;

            /// <summary>Coins the helpers share, per helper, by how much each did.</summary>
            public long HelperCoinsPerHelper = 500;

            /// <summary>A story the streamer must have finished before this one can begin.</summary>
            public string Requires;
        }

        // Written 2026-10-25. Numbers to be balanced: a gathering round comes every 10 seconds per player, and a
        // find chance of 1/150 is about one find per 25 minutes of one gatherer.
        public static readonly Story[] All =
        {
            // The runestone in the Ironhill cave: "Power is emerging from this area... But nothing seem to be happening."
            new Story
            {
                Key = "runestone",
                Steps = new[]
                {
                    new Step { Kind = Talk },
                    new Step { Kind = QuestManager.Kill, Amount = 300 },
                    new Step { Kind = Deliver, Target = "Iron Ore", Amount = 200 },
                    new Step { Kind = Deliver, Target = "Coal", Amount = 120 },
                    new Step { Kind = Find, Skill = (int)Skill.Mining, Amount = 3, Chance = 1.0 / 150 },
                    new Step { Kind = QuestManager.Dungeon, Amount = 2 },
                    new Step { Kind = Choice, Options = 3 },
                    new Step { Kind = Talk },
                },
                Unlocks = new[] { "title_runebreaker", "runestone_open" },
                Perk = "perk_runestone_",
                PerkChoiceStep = 6,
            },

            // The mangrove island north east of the main island, where a sign says "insufficient bananas".
            new Story
            {
                Key = "bananas",
                Steps = new[]
                {
                    new Step { Kind = Talk },
                    new Step { Kind = Find, Skill = (int)Skill.Gathering, Amount = 25, Chance = 1.0 / 120 },
                    new Step { Kind = Viewers, Amount = 5 },
                    new Step { Kind = Deliver, Target = "Raw Sprat", Amount = 150 },
                    new Step { Kind = QuestManager.Raid, Amount = 2 },
                    new Step { Kind = Choice, Options = 3 },
                    new Step { Kind = Talk },
                },
                Unlocks = new[] { "island_mangrove", "title_monkey_friend" },
                Perk = "perk_bananas",
            },

            // The awakened runestone hums toward the west, where Ravebridge lies (phase 6, 2026-10-25): it opens the
            // Ravebridge event for the stream. Torn pieces of an old map turn up while gathering; the crossing needs
            // planks, and tin for the nails.
            new Story
            {
                Key = "roadwest",
                Requires = "runestone",
                Steps = new[]
                {
                    new Step { Kind = Talk },
                    new Step { Kind = Find, Skill = (int)Skill.Woodcutting, Amount = 4, Chance = 1.0 / 150 },
                    new Step { Kind = QuestManager.Dungeon, Amount = 2 },
                    new Step { Kind = Deliver, Target = "Wood Plank", Amount = 150 },
                    new Step { Kind = Deliver, Target = "Tin Ore", Amount = 80 },
                    new Step { Kind = Talk },
                },
                Unlocks = new[] { "island_ravebridge" },
            },
        };

        public static Story Get(string key) => All.FirstOrDefault(s => s.Key == key);
    }
}

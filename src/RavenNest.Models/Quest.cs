using System;

namespace RavenNest.Models
{
    /// <summary>A character's quest as the game shows it; the game words it from Key and the numbers.</summary>
    public class QuestInfo
    {
        public Guid Id { get; set; }
        public string Key { get; set; }

        /// <summary>0 daily, 1 weekly.</summary>
        public int Kind { get; set; }

        /// <summary>Which of the day's offers (0 to 2).</summary>
        public int Slot { get; set; }

        /// <summary>gather, exp, craft, cook, brew, kill, raid or dungeon.</summary>
        public string Objective { get; set; }

        /// <summary>The skill it is about (its index), or -1.</summary>
        public int Skill { get; set; }
        public long Amount { get; set; }
        public long Progress { get; set; }

        /// <summary>0 offered, 1 taken, 2 finished, 3 expired, 4 given up.</summary>
        public int State { get; set; }
        public long RewardCoins { get; set; }
        public double RewardExp { get; set; }
        public int RewardSkill { get; set; }
        public DateTime Expires { get; set; }
    }

    /// <summary>A character's quests: the taken ones, today's offers, this week's quest and the daily streak.</summary>
    public class QuestList
    {
        public QuestInfo[] Active { get; set; }
        public QuestInfo[] Offers { get; set; }
        public QuestInfo Weekly { get; set; }
        public int Streak { get; set; }
        public long DailiesCompleted { get; set; }
    }

    public class QuestActionResult
    {
        public bool Ok { get; set; }

        /// <summary>Why not: unknown, full, taken, done, expired, notinsession.</summary>
        public string Error { get; set; }
        public QuestInfo Quest { get; set; }
    }

    /// <summary>A stream quest: the goal, how far the stream is, the time left and the rewards.</summary>
    public class StreamQuestInfo
    {
        public Guid Id { get; set; }
        public string Key { get; set; }

        /// <summary>Which of today's offers (0 to 2) when offered.</summary>
        public int Slot { get; set; }

        /// <summary>gather, kill, make, levels, raid or dungeon.</summary>
        public string Objective { get; set; }
        public int Skill { get; set; }
        public long Amount { get; set; }
        public long Progress { get; set; }

        /// <summary>0 offered, 1 running, 2 done, 3 out of time, 4 stopped.</summary>
        public int State { get; set; }
        public int Minutes { get; set; }
        public DateTime Ends { get; set; }
        public long RewardVillageCoins { get; set; }

        /// <summary>1 wood, 2 ore, 3 fish, 4 wheat, 0 none.</summary>
        public int RewardResource { get; set; }
        public long RewardResourceAmount { get; set; }
        public long RewardHelperCoins { get; set; }
        public int Helpers { get; set; }
        public StreamQuestHelper[] TopHelpers { get; set; }
    }

    public class StreamQuestHelper
    {
        public Guid CharacterId { get; set; }
        public string Name { get; set; }
        public long Amount { get; set; }

        /// <summary>The coins it got, once the quest is done.</summary>
        public long Coins { get; set; }
    }

    /// <summary>The stream's quest now, if one runs, and today's offers for the streamer to start.</summary>
    public class StreamQuestState
    {
        public StreamQuestInfo Active { get; set; }
        public StreamQuestInfo[] Offers { get; set; }
    }

    public class StreamQuestResult
    {
        public bool Ok { get; set; }

        /// <summary>Why not: running (one already runs), unknown, none (nothing to stop).</summary>
        public string Error { get; set; }
        public StreamQuestInfo Quest { get; set; }
    }

    /// <summary>The StreamQuestEnded event's data: the quest as it ended, and every helper with their coins (for the game to add).</summary>
    public class StreamQuestEnded
    {
        public StreamQuestInfo Quest { get; set; }
        public StreamQuestHelper[] Helpers { get; set; }
    }

    /// <summary>The QuestCompleted event's data: the quest, its rewards (coins already given; the game adds the experience) and the streak.</summary>
    public class QuestCompleted
    {
        public Guid CharacterId { get; set; }
        public QuestInfo Quest { get; set; }
        public int Streak { get; set; }
    }
}

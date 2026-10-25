using System;

namespace RavenNest.Models
{
    /// <summary>A character's quest as the game shows it; the game words it from Key and the numbers.</summary>
    public class QuestInfo
    {
        public Guid Id { get; set; }
        public string Key { get; set; }

        /// <summary>0 daily, 1 weekly, 2 seasonal daily, 3 chain.</summary>
        public int Kind { get; set; }

        /// <summary>Which of the day's offers (0 to 2).</summary>
        public int Slot { get; set; }

        /// <summary>gather, exp, craft, cook, brew, kill, raid, dungeon, or for chain steps deliver, visit or answer.</summary>
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

        /// <summary>A chain's current step (from 0) and how many it has.</summary>
        public int Step { get; set; }
        public int Steps { get; set; }

        /// <summary>A chain step's item to deliver ("Raw Sprat") or island to visit ("Kyo").</summary>
        public string Target { get; set; }

        /// <summary>An answer step's number of choices.</summary>
        public int Options { get; set; }

        /// <summary>A chain's title and village decor piece, given when it is finished.</summary>
        public string RewardTitle { get; set; }
        public string RewardDecor { get; set; }
    }

    /// <summary>A quest chain as !quest chains shows it: whether it can be begun, how far it is, or done.</summary>
    public class QuestChainInfo
    {
        public string Key { get; set; }

        /// <summary>0 can be begun, 1 under way, 2 finished, 5 not now (a seasonal chain outside its event).</summary>
        public int State { get; set; }
        public int Step { get; set; }
        public int Steps { get; set; }

        /// <summary>The event it belongs to (halloween), or null.</summary>
        public string Event { get; set; }
        public string RewardTitle { get; set; }
        public string RewardDecor { get; set; }
    }

    /// <summary>A character's quests: the taken ones, today's offers, this week's quest and the daily streak.</summary>
    public class QuestList
    {
        public QuestInfo[] Active { get; set; }
        public QuestInfo[] Offers { get; set; }
        public QuestInfo Weekly { get; set; }
        public int Streak { get; set; }
        public long DailiesCompleted { get; set; }

        /// <summary>Today's seasonal quest while an event runs, or null.</summary>
        public QuestInfo Seasonal { get; set; }

        /// <summary>The chain under way, or null, and every chain.</summary>
        public QuestInfo Chain { get; set; }
        public QuestChainInfo[] Chains { get; set; }
    }

    public class QuestActionResult
    {
        public bool Ok { get; set; }

        /// <summary>Why not: unknown, full, taken, done, expired, notinsession, busy (a chain under way), notnow,
        /// nothing (no items to deliver), wrong (a wrong answer), notstep (the step is not that kind).</summary>
        public string Error { get; set; }
        public QuestInfo Quest { get; set; }

        /// <summary>How many items a delivery took.</summary>
        public long Delivered { get; set; }
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

        /// <summary>The quest; for a chain step that was not the last, the chain at its next step (State 1).</summary>
        public QuestInfo Quest { get; set; }
        public int Streak { get; set; }

        /// <summary>For a chain: the step that was just finished.</summary>
        public int StepDone { get; set; }

        /// <summary>A finished chain's village decor piece, when this village did not have it yet.</summary>
        public string DecorUnlocked { get; set; }
    }

    /// <summary>One achievement as the game shows it: progress, the tier reached and the next target.</summary>
    public class AchievementInfo
    {
        public string Key { get; set; }
        public long Progress { get; set; }

        /// <summary>0 none, 1 bronze, 2 silver, 3 gold.</summary>
        public int Tier { get; set; }
        public long[] Targets { get; set; }
        public bool Hidden { get; set; }
    }

    /// <summary>A character's achievements, the titles earned and the one worn.</summary>
    public class AchievementList
    {
        public AchievementInfo[] Achievements { get; set; }
        public string[] Titles { get; set; }
        public string Title { get; set; }
    }

    public class TitleResult
    {
        public bool Ok { get; set; }

        /// <summary>notearned, unknown, notinsession.</summary>
        public string Error { get; set; }
        public string Title { get; set; }
    }

    /// <summary>The titles worn by the characters in a stream, for their name tags.</summary>
    public class SessionTitles
    {
        public Guid[] CharacterIds { get; set; }
        public string[] Titles { get; set; }
    }

    /// <summary>A character reached an achievement's tier (AchievementReached): the title that came with it.</summary>
    public class AchievementReached
    {
        public Guid CharacterId { get; set; }
        public string Key { get; set; }
        public int Tier { get; set; }
        public string Title { get; set; }
    }
}

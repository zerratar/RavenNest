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

    /// <summary>A streamer's quest as the game shows it (phase 4): a bounty or a stream goal with its reward set aside.</summary>
    public class StreamerQuestInfo
    {
        public Guid Id { get; set; }

        /// <summary>1 bounty, 2 stream goal.</summary>
        public int Kind { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }

        /// <summary>gather, kill, craft, cook, brew, levels, raid or dungeon.</summary>
        public string Objective { get; set; }
        public int Skill { get; set; }

        /// <summary>Per viewer for a bounty, for the whole stream for a goal.</summary>
        public long Amount { get; set; }
        public long Progress { get; set; }
        public int Winners { get; set; }
        public int Claimed { get; set; }

        /// <summary>1 running, 2 done, 3 out of time, 4 stopped, 5 every reward claimed.</summary>
        public int State { get; set; }
        public DateTime Ends { get; set; }
        public int Minutes { get; set; }
        public long RewardCoins { get; set; }
        public Guid? RewardItemId { get; set; }
        public string RewardItemName { get; set; }
        public long RewardItemAmount { get; set; }
        public long RewardLoyalty { get; set; }
        public int Helpers { get; set; }
        public StreamQuestHelper[] TopHelpers { get; set; }

        /// <summary>When asked for one character (!quest): its progress, and whether it has claimed the bounty.</summary>
        public long YourProgress { get; set; }
        public bool YouClaimed { get; set; }
    }

    /// <summary>What a streamer posts from the quest window.</summary>
    public class StreamerQuestRequest
    {
        public int Kind { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Objective { get; set; }
        public int Skill { get; set; }
        public long Amount { get; set; }
        public int Minutes { get; set; }
        public int Winners { get; set; }
        public long RewardCoins { get; set; }

        /// <summary>1 the village treasury, 2 the streamer's character (CoinCharacterId).</summary>
        public int CoinSource { get; set; }
        public Guid? CoinCharacterId { get; set; }

        /// <summary>The item, as the tavern shelves name a stack, and where it is taken from.</summary>
        public Guid? ItemId { get; set; }
        public string ItemTag { get; set; }
        public string ItemEnchantment { get; set; }
        public Guid? ItemTransmogrificationId { get; set; }
        public string ItemName { get; set; }
        public int ItemFlags { get; set; }
        public long ItemAmount { get; set; }
        public StreamerShopSourceKind ItemSourceKind { get; set; }
        public Guid? ItemSourceCharacterId { get; set; }
        public long RewardLoyalty { get; set; }
    }

    /// <summary>The stream's streamer quests and what the streamer can set aside for a new one.</summary>
    public class StreamerQuestList
    {
        public StreamerQuestInfo[] Quests { get; set; }
        public int MaxBounties { get; set; }
        public long LoyaltyBudgetLeft { get; set; }
        public long LoyaltyBudgetWeekly { get; set; }
        public long TreasuryCoins { get; set; }

        /// <summary>The streamer's characters in the stream, and their coins.</summary>
        public StreamerQuestCoinSource[] Characters { get; set; }
    }

    public class StreamerQuestCoinSource
    {
        public Guid CharacterId { get; set; }
        public string Name { get; set; }
        public long Coins { get; set; }
    }

    public class StreamerQuestResult
    {
        public bool Ok { get; set; }

        /// <summary>A sentence for the streamer when it did not go.</summary>
        public string Error { get; set; }
        public StreamerQuestInfo Quest { get; set; }
    }

    /// <summary>A viewer finished a bounty (StreamerQuestClaimed): what they got, and how many rewards are left.</summary>
    public class StreamerQuestClaimed
    {
        public StreamerQuestInfo Quest { get; set; }
        public Guid CharacterId { get; set; }
        public long Coins { get; set; }
        public long Items { get; set; }
        public long Loyalty { get; set; }
    }

    /// <summary>A streamer quest ended (StreamerQuestEnded); for a goal that was done, what each helper got.</summary>
    public class StreamerQuestEnded
    {
        public StreamerQuestInfo Quest { get; set; }
        public StreamerQuestReward[] Rewards { get; set; }
    }

    public class StreamerQuestReward
    {
        public Guid CharacterId { get; set; }
        public string Name { get; set; }
        public long Amount { get; set; }
        public long Coins { get; set; }
        public long Items { get; set; }
        public long Loyalty { get; set; }
    }

    /// <summary>One of the streamer's story lines (phase 5) as the game shows it, with its current step.</summary>
    public class VillageStoryInfo
    {
        public string Key { get; set; }

        /// <summary>0 not begun, 1 under way, 2 finished, 4 given up.</summary>
        public int State { get; set; }
        public int Step { get; set; }
        public int Steps { get; set; }

        /// <summary>The step's kind: talk, deliver, gather, kill, raid, dungeon, find, viewers or choice.</summary>
        public string Kind { get; set; }

        /// <summary>A delivery's item ("Iron Ore").</summary>
        public string Target { get; set; }
        public int Skill { get; set; }
        public long Amount { get; set; }
        public long Progress { get; set; }

        /// <summary>A choice step's number of options.</summary>
        public int Options { get; set; }

        /// <summary>The choices made, by step: "6=2", comma separated.</summary>
        public string Choices { get; set; }

        /// <summary>Who helped most with this step, and for a find step who found each.</summary>
        public StreamQuestHelper[] Helpers { get; set; }
    }

    /// <summary>The streamer's story lines and what they have earned.</summary>
    public class VillageLog
    {
        public VillageStoryInfo[] Stories { get; set; }

        /// <summary>island_mangrove, perk_runestone_nature, title_runebreaker, ...</summary>
        public string[] Unlocks { get; set; }
    }

    public class VillageStoryResult
    {
        public bool Ok { get; set; }

        /// <summary>busy, done, unknown, notstep, nothing, notinsession.</summary>
        public string Error { get; set; }
        public VillageStoryInfo Story { get; set; }

        /// <summary>How many items !quest give handed in.</summary>
        public long Given { get; set; }
    }

    /// <summary>A step of a story line moved (VillageStoryProgress): something found (Finder), or a step done (StepDone).</summary>
    /// <summary>A world event pulls a stream to Ravebridge (GameEventType.RavebridgeForced).</summary>
    public class RavebridgeForced
    {
        /// <summary>What the stream is told, for example "The goblins of Ravebridge are rising".</summary>
        public string Reason { get; set; }
    }

    public class VillageStoryProgress
    {
        public VillageStoryInfo Story { get; set; }

        /// <summary>The step that was just finished, or -1.</summary>
        public int StepDone { get; set; }
        public Guid FinderId { get; set; }
        public string FinderName { get; set; }
    }

    /// <summary>A story line was finished (VillageStoryCompleted).</summary>
    public class VillageStoryCompleted
    {
        public VillageStoryInfo Story { get; set; }
        public string[] Unlocks { get; set; }
        public string DecorUnlocked { get; set; }
        public StreamQuestHelper[] Helpers { get; set; }
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
